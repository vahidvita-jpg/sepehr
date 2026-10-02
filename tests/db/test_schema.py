"""Database tests: apply migrations to a fresh SQLite DB, load demo seed, verify constraints.
Run:  python3 tests/db/test_schema.py"""
import glob, os, sqlite3, sys, tempfile, unittest

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))

def migrate(con):
    con.execute("PRAGMA foreign_keys=ON")
    done = set()
    try: done = {r[0] for r in con.execute("SELECT version FROM SchemaMigrations")}
    except sqlite3.OperationalError: pass
    for f in sorted(glob.glob(os.path.join(ROOT, "db/migrations/*.sql"))):
        v = int(os.path.basename(f).split("_")[0])
        if v not in done:
            con.executescript(open(f, encoding="utf-8").read())

def fresh(demo=True):
    path = os.path.join(tempfile.mkdtemp(), "t.db")
    con = sqlite3.connect(path); con.execute("PRAGMA journal_mode=WAL")
    migrate(con)
    if demo: con.executescript(open(os.path.join(ROOT, "db/demo/demo_seed.sql"), encoding="utf-8").read())
    con.execute("PRAGMA foreign_keys=ON")
    return con

class T(unittest.TestCase):
    def setUp(self): self.con = fresh()
    def q1(self, sql, *a): return self.con.execute(sql, a).fetchone()[0]
    def fails(self, sql, *a, msg=None):
        with self.assertRaises(sqlite3.DatabaseError) as cm: self.con.execute(sql, a)
        if msg: self.assertIn(msg, str(cm.exception))

    def test_migration_idempotent_and_versions(self):
        migrate(self.con)
        self.assertEqual(self.q1("SELECT COUNT(*) FROM SchemaMigrations"), 2)

    def test_demo_counts(self):
        self.assertEqual(self.q1("SELECT COUNT(*) FROM Schools"), 1)
        self.assertEqual(self.q1("SELECT COUNT(*) FROM Teachers"), 10)
        self.assertEqual(self.q1("SELECT COUNT(DISTINCT grade_level) FROM Classes"), 3)
        self.assertEqual(self.q1("SELECT COUNT(*) FROM Students"), 30)
        self.assertEqual(self.q1("SELECT value FROM Settings WHERE key='data_mode'"), "demo")
        self.assertEqual(self.q1("SELECT is_demo FROM Schools"), 1)

    def test_integrity(self):
        self.assertEqual(self.q1("PRAGMA integrity_check"), "ok")
        self.assertEqual(self.con.execute("PRAGMA foreign_key_check").fetchall(), [])

    def test_no_plaintext_demo_passwords(self):
        self.assertEqual(self.q1("SELECT COUNT(*) FROM Users WHERE password_hash NOT LIKE '!DEMO-DISABLED!'"), 0)

    def test_fk_enforced(self):
        self.fails("INSERT INTO Grades(student_id,subject_id,teacher_id,academic_year_id,kind,score,graded_on,persian_date) VALUES (999,1,1,1,'classroom',15,'2026-10-01','1405/07/09')")

    def test_grade_range(self):
        base = "INSERT INTO Grades(student_id,subject_id,teacher_id,academic_year_id,kind,score,graded_on,persian_date) VALUES (1,1,1,1,'classroom',?,'2026-10-01','1405/07/09')"
        self.con.execute(base, (18.5,))
        self.fails(base, 21); self.fails(base, -1)

    def test_teacher_double_booking_blocked(self):
        # teacher 1 already teaches somewhere in day0/bell2; put him in another class same slot
        row = self.con.execute("SELECT teacher_id,day_of_week,bell_schedule_id,class_id FROM Timetables WHERE id=1").fetchone()
        other = self.q1("SELECT id FROM Classes WHERE id<>?", row[3])
        self.fails("INSERT INTO Timetables(academic_year_id,class_id,teacher_id,subject_id,day_of_week,bell_schedule_id) VALUES (1,?,?,1,?,?)",
                   other, row[0], row[1], row[2])

    def test_demo_timetable_complete(self):
        self.assertEqual(self.q1("SELECT COUNT(*) FROM Timetables"), 6*5*4)

    def test_attendance_requires_timetable(self):
        # 2026-10-03 is a Saturday -> day_of_week 0, teachers have lessons
        sat = "2026-10-03"; fri = "2026-10-02"        # Friday = day 6, no lessons in demo
        ins = "INSERT INTO Attendance(teacher_id,academic_year_id,attendance_date,persian_date,status) VALUES (1,1,?,?, 'present')"
        self.con.execute(ins, (sat, "1405/07/11"))
        self.fails(ins, fri, "1405/07/10", msg="NO_TIMETABLE_FOR_DAY")
        self.fails(ins, sat, "1405/07/11")             # duplicate for same day

    def test_persian_date_format_check(self):
        self.fails("INSERT INTO Attendance(teacher_id,academic_year_id,attendance_date,persian_date) VALUES (1,1,'2026-10-03','2026-10-03')")

    def test_duplicates_rejected(self):
        self.fails("INSERT INTO Students(school_id,student_code,national_id,first_name,last_name) VALUES (1,'S5001','0000000000','a','b')")
        nid = self.q1("SELECT national_id FROM Students WHERE id=1")
        self.fails("INSERT INTO Students(school_id,student_code,national_id,first_name,last_name) VALUES (1,'NEW',?, 'a','b')", nid)
        self.fails("INSERT INTO Teachers(school_id,first_name,last_name,national_id) VALUES (1,'a','b',?)",
                   self.q1("SELECT national_id FROM Teachers WHERE id=1"))

    def test_input_checks(self):
        self.fails("INSERT INTO Teachers(school_id,first_name,last_name,national_id) VALUES (1,'a','b','12345')")
        self.fails("INSERT INTO Teachers(school_id,first_name,last_name,national_id,mobile) VALUES (1,'a','b','1111111111','12345')")

    def test_audit_immutable(self):
        self.con.execute("INSERT INTO AuditLogs(user_id,action) VALUES (1,'login')")
        self.fails("UPDATE AuditLogs SET action='x'", msg="AUDIT_LOG_IMMUTABLE")
        self.fails("DELETE FROM AuditLogs", msg="AUDIT_LOG_IMMUTABLE")

    def test_permissions_and_roles(self):
        n = lambda r: self.q1("SELECT COUNT(*) FROM RolePermissions WHERE role_id=?", r)
        total = self.q1("SELECT COUNT(*) FROM Permissions")
        self.assertEqual(n(1), total); self.assertLess(n(2), total)
        self.assertEqual(self.q1("SELECT COUNT(*) FROM RolePermissions rp JOIN Permissions p ON p.id=rp.permission_id WHERE rp.role_id=3 AND p.key='grades.edit'"), 0)

    def test_one_current_year(self):
        self.fails("INSERT INTO AcademicYears(school_id,label,start_date,end_date,is_current) VALUES (1,'1406-1407','2027-09-23','2028-06-21',1)")

    def test_attachment_limits(self):
        self.con.execute("INSERT INTO Messages(sender_user_id,recipient_user_id,body) VALUES (1,2,'hi')")
        ins = "INSERT INTO MessageAttachments(message_id,original_name,stored_path,extension,size_bytes,sha256) VALUES (1,'a','p',?,?,'h')"
        self.con.execute(ins, ("pdf", 1000))
        self.fails(ins, "exe", 1000); self.fails(ins, "pdf", 30*1024*1024)
        self.fails("INSERT INTO Messages(sender_user_id,recipient_user_id,body) VALUES (1,1,'self')")

    def test_index_used_for_class_roster(self):
        plan = " ".join(r[3] for r in self.con.execute("EXPLAIN QUERY PLAN SELECT * FROM Students WHERE class_id=3"))
        self.assertIn("ix_students_class", plan)

    def test_online_backup_roundtrip(self):
        dst = sqlite3.connect(os.path.join(tempfile.mkdtemp(), "b.db"))
        self.con.commit(); self.con.backup(dst)
        self.assertEqual(dst.execute("SELECT COUNT(*) FROM Students").fetchone()[0], 30)
        self.assertEqual(dst.execute("PRAGMA integrity_check").fetchone()[0], "ok")

    def test_production_db_has_no_demo_data(self):
        c = fresh(demo=False)
        self.assertEqual(c.execute("SELECT COUNT(*) FROM Students").fetchone()[0], 0)
        self.assertEqual(c.execute("SELECT value FROM Settings WHERE key='data_mode'").fetchone()[0], "production")

if __name__ == "__main__":
    unittest.main(verbosity=2)
