-- Sepehr School Management System - migration 0001 (SQLite, WAL)
-- Conventions: dates stored as ISO-8601 Gregorian (YYYY-MM-DD), times as HH:MM[:SS],
-- timestamps UTC ISO. Persian dates stored alongside where the spec requires them.
-- day_of_week: 0=Saturday ... 6=Friday (Iranian school week).
PRAGMA foreign_keys = ON;

CREATE TABLE SchemaMigrations (
  version     INTEGER PRIMARY KEY,
  name        TEXT NOT NULL,
  applied_at  TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%SZ','now'))
);

CREATE TABLE Schools (
  id INTEGER PRIMARY KEY,
  name TEXT NOT NULL,
  code TEXT UNIQUE,
  address TEXT, phone TEXT, principal_name TEXT, contact_info TEXT,
  logo_path TEXT, background_path TEXT,
  is_demo INTEGER NOT NULL DEFAULT 0 CHECK (is_demo IN (0,1)),
  created_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%SZ','now'))
);

CREATE TABLE AcademicYears (
  id INTEGER PRIMARY KEY,
  school_id INTEGER NOT NULL REFERENCES Schools(id),
  label TEXT NOT NULL,                       -- e.g. 1405-1406
  start_date TEXT NOT NULL, end_date TEXT NOT NULL,
  is_current INTEGER NOT NULL DEFAULT 0 CHECK (is_current IN (0,1)),
  CHECK (end_date > start_date),
  UNIQUE (school_id, label)
);
CREATE UNIQUE INDEX ux_academicyears_current ON AcademicYears(school_id) WHERE is_current = 1;

CREATE TABLE Roles (
  id INTEGER PRIMARY KEY,
  name TEXT NOT NULL UNIQUE,                 -- SuperAdmin, Principal, Teacher, Staff
  is_system INTEGER NOT NULL DEFAULT 0
);
CREATE TABLE Permissions (
  id INTEGER PRIMARY KEY,
  key TEXT NOT NULL UNIQUE,                  -- e.g. grades.edit
  description TEXT
);
CREATE TABLE RolePermissions (
  role_id INTEGER NOT NULL REFERENCES Roles(id) ON DELETE CASCADE,
  permission_id INTEGER NOT NULL REFERENCES Permissions(id) ON DELETE CASCADE,
  PRIMARY KEY (role_id, permission_id)
);

CREATE TABLE Users (
  id INTEGER PRIMARY KEY,
  school_id INTEGER NOT NULL REFERENCES Schools(id),
  username TEXT NOT NULL UNIQUE COLLATE NOCASE,
  password_hash TEXT NOT NULL,               -- PBKDF2/BCrypt string; never plain text
  role_id INTEGER NOT NULL REFERENCES Roles(id),
  display_name TEXT NOT NULL,
  is_active INTEGER NOT NULL DEFAULT 1 CHECK (is_active IN (0,1)),
  failed_attempts INTEGER NOT NULL DEFAULT 0,
  locked_until TEXT,
  last_login_at TEXT,
  created_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%SZ','now'))
);
CREATE TABLE UserSessions (
  id INTEGER PRIMARY KEY,
  user_id INTEGER NOT NULL REFERENCES Users(id) ON DELETE CASCADE,
  refresh_token_hash TEXT NOT NULL UNIQUE,
  device_info TEXT, ip_address TEXT,
  created_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%SZ','now')),
  expires_at TEXT NOT NULL, revoked_at TEXT
);
CREATE INDEX ix_sessions_user ON UserSessions(user_id);

CREATE TABLE Subjects (
  id INTEGER PRIMARY KEY,
  school_id INTEGER NOT NULL REFERENCES Schools(id),
  name TEXT NOT NULL, department TEXT,
  UNIQUE (school_id, name)
);

CREATE TABLE Teachers (
  id INTEGER PRIMARY KEY,
  school_id INTEGER NOT NULL REFERENCES Schools(id),
  user_id INTEGER UNIQUE REFERENCES Users(id),
  first_name TEXT NOT NULL, last_name TEXT NOT NULL,
  national_id TEXT NOT NULL UNIQUE CHECK (length(national_id)=10 AND national_id NOT GLOB '*[^0-9]*'),
  employee_id TEXT UNIQUE,
  mobile TEXT CHECK (mobile IS NULL OR mobile GLOB '09[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]'),
  email TEXT, photo_path TEXT,
  primary_subject_id INTEGER REFERENCES Subjects(id),
  employment_type TEXT NOT NULL DEFAULT 'official' CHECK (employment_type IN ('official','contract','hourly','substitute')),
  status TEXT NOT NULL DEFAULT 'active' CHECK (status IN ('active','inactive','leave')),
  notes TEXT,
  created_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%SZ','now'))
);
CREATE INDEX ix_teachers_name ON Teachers(last_name, first_name);

CREATE TABLE Classes (
  id INTEGER PRIMARY KEY,
  school_id INTEGER NOT NULL REFERENCES Schools(id),
  academic_year_id INTEGER NOT NULL REFERENCES AcademicYears(id),
  grade_level INTEGER NOT NULL CHECK (grade_level BETWEEN 1 AND 12),
  section TEXT NOT NULL,
  room TEXT, shift_id INTEGER REFERENCES Shifts(id),
  UNIQUE (academic_year_id, grade_level, section)
);

CREATE TABLE TeacherSubjects (
  teacher_id INTEGER NOT NULL REFERENCES Teachers(id) ON DELETE CASCADE,
  subject_id INTEGER NOT NULL REFERENCES Subjects(id) ON DELETE CASCADE,
  PRIMARY KEY (teacher_id, subject_id)
);
CREATE TABLE TeacherClasses (
  teacher_id INTEGER NOT NULL REFERENCES Teachers(id) ON DELETE CASCADE,
  class_id INTEGER NOT NULL REFERENCES Classes(id) ON DELETE CASCADE,
  subject_id INTEGER NOT NULL REFERENCES Subjects(id),
  PRIMARY KEY (teacher_id, class_id, subject_id)
);

CREATE TABLE Students (
  id INTEGER PRIMARY KEY,
  school_id INTEGER NOT NULL REFERENCES Schools(id),
  student_code TEXT NOT NULL,
  national_id TEXT NOT NULL CHECK (length(national_id)=10 AND national_id NOT GLOB '*[^0-9]*'),
  first_name TEXT NOT NULL, last_name TEXT NOT NULL, father_name TEXT,
  birth_date TEXT, class_id INTEGER REFERENCES Classes(id),
  parent_name TEXT,
  parent_phone TEXT CHECK (parent_phone IS NULL OR parent_phone GLOB '09[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]'),
  address TEXT, photo_path TEXT,
  status TEXT NOT NULL DEFAULT 'active' CHECK (status IN ('active','transferred','graduated','inactive')),
  created_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%SZ','now')),
  UNIQUE (school_id, student_code),
  UNIQUE (school_id, national_id)
);
CREATE INDEX ix_students_class ON Students(class_id);
CREATE INDEX ix_students_name ON Students(last_name, first_name);

CREATE TABLE Shifts (
  id INTEGER PRIMARY KEY,
  school_id INTEGER NOT NULL REFERENCES Schools(id),
  name TEXT NOT NULL, UNIQUE (school_id, name)
);
CREATE TABLE BellSounds (
  id INTEGER PRIMARY KEY,
  school_id INTEGER NOT NULL REFERENCES Schools(id),
  name TEXT NOT NULL, file_path TEXT NOT NULL,
  format TEXT NOT NULL CHECK (format IN ('mp3','wav','ogg','m4a')),
  sha256 TEXT, size_bytes INTEGER CHECK (size_bytes IS NULL OR size_bytes BETWEEN 1 AND 52428800)
);
CREATE TABLE BellSchedules (
  id INTEGER PRIMARY KEY,
  shift_id INTEGER NOT NULL REFERENCES Shifts(id) ON DELETE CASCADE,
  period_no INTEGER,                          -- NULL for non-teaching events
  kind TEXT NOT NULL CHECK (kind IN ('class','break','prayer','opening','closing','custom')),
  label TEXT NOT NULL,
  start_time TEXT NOT NULL CHECK (start_time GLOB '[0-2][0-9]:[0-5][0-9]*'),
  end_time   TEXT NOT NULL CHECK (end_time   GLOB '[0-2][0-9]:[0-5][0-9]*'),
  sound_id INTEGER REFERENCES BellSounds(id) ON DELETE SET NULL,
  volume INTEGER NOT NULL DEFAULT 80 CHECK (volume BETWEEN 0 AND 100),
  enabled INTEGER NOT NULL DEFAULT 1 CHECK (enabled IN (0,1)),
  CHECK (end_time >= start_time)
);
CREATE INDEX ix_bell_shift_time ON BellSchedules(shift_id, start_time);

CREATE TABLE MediaPlaylists (
  id INTEGER PRIMARY KEY,
  school_id INTEGER NOT NULL REFERENCES Schools(id),
  category TEXT NOT NULL CHECK (category IN ('anthem','quran','quran_translation','morning_prayer','announcements','adhan','other')),
  name TEXT NOT NULL, shift_id INTEGER REFERENCES Shifts(id),
  schedule_time TEXT, volume INTEGER NOT NULL DEFAULT 80 CHECK (volume BETWEEN 0 AND 100)
);
CREATE TABLE MediaPlaylistItems (
  id INTEGER PRIMARY KEY,
  playlist_id INTEGER NOT NULL REFERENCES MediaPlaylists(id) ON DELETE CASCADE,
  sound_id INTEGER NOT NULL REFERENCES BellSounds(id),
  position INTEGER NOT NULL, UNIQUE (playlist_id, position)
);

CREATE TABLE Timetables (
  id INTEGER PRIMARY KEY,
  academic_year_id INTEGER NOT NULL REFERENCES AcademicYears(id),
  class_id INTEGER NOT NULL REFERENCES Classes(id) ON DELETE CASCADE,
  teacher_id INTEGER NOT NULL REFERENCES Teachers(id),
  subject_id INTEGER NOT NULL REFERENCES Subjects(id),
  day_of_week INTEGER NOT NULL CHECK (day_of_week BETWEEN 0 AND 6),
  bell_schedule_id INTEGER NOT NULL REFERENCES BellSchedules(id),
  room TEXT,
  UNIQUE (class_id, day_of_week, bell_schedule_id),     -- a class has one lesson per slot
  UNIQUE (teacher_id, day_of_week, bell_schedule_id)    -- a teacher cannot be double-booked
);
CREATE INDEX ix_timetable_teacher_day ON Timetables(teacher_id, academic_year_id, day_of_week);

CREATE TABLE Attendance (                      -- teacher attendance
  id INTEGER PRIMARY KEY,
  teacher_id INTEGER NOT NULL REFERENCES Teachers(id),
  academic_year_id INTEGER NOT NULL REFERENCES AcademicYears(id),
  attendance_date TEXT NOT NULL,
  persian_date TEXT NOT NULL CHECK (persian_date GLOB '[0-9][0-9][0-9][0-9]/[0-1][0-9]/[0-3][0-9]'),
  check_in_at TEXT, check_out_at TEXT,
  status TEXT NOT NULL DEFAULT 'present' CHECK (status IN ('present','late','absent','leave')),
  signature BLOB, signed_at TEXT,
  device_info TEXT, ip_address TEXT,
  created_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%SZ','now')),
  UNIQUE (teacher_id, attendance_date)
);
CREATE INDEX ix_attendance_date ON Attendance(attendance_date);

CREATE TABLE StudentAttendance (
  id INTEGER PRIMARY KEY,
  student_id INTEGER NOT NULL REFERENCES Students(id),
  academic_year_id INTEGER NOT NULL REFERENCES AcademicYears(id),
  recorded_by_user_id INTEGER NOT NULL REFERENCES Users(id),
  event_date TEXT NOT NULL, persian_date TEXT NOT NULL, event_time TEXT,
  kind TEXT NOT NULL CHECK (kind IN ('absent_excused','absent_unexcused','late','early_departure')),
  reason TEXT,
  created_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%SZ','now'))
);
CREATE INDEX ix_stuatt_student ON StudentAttendance(student_id, event_date);

CREATE TABLE Grades (
  id INTEGER PRIMARY KEY,
  student_id INTEGER NOT NULL REFERENCES Students(id),
  subject_id INTEGER NOT NULL REFERENCES Subjects(id),
  teacher_id INTEGER NOT NULL REFERENCES Teachers(id),
  academic_year_id INTEGER NOT NULL REFERENCES AcademicYears(id),
  kind TEXT NOT NULL CHECK (kind IN ('classroom','exam','quiz','final')),
  score REAL NOT NULL CHECK (score BETWEEN 0 AND 20),   -- Iranian 0-20 scale
  graded_on TEXT NOT NULL, persian_date TEXT NOT NULL, note TEXT,
  created_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%SZ','now')),
  updated_at TEXT
);
CREATE INDEX ix_grades_student ON Grades(student_id, academic_year_id, subject_id);
CREATE INDEX ix_grades_teacher ON Grades(teacher_id, graded_on);

CREATE TABLE DisciplineRecords (
  id INTEGER PRIMARY KEY,
  student_id INTEGER NOT NULL REFERENCES Students(id),
  teacher_id INTEGER REFERENCES Teachers(id),
  academic_year_id INTEGER NOT NULL REFERENCES AcademicYears(id),
  event_date TEXT NOT NULL, persian_date TEXT NOT NULL, event_time TEXT,
  violation_type TEXT NOT NULL, description TEXT,
  severity INTEGER NOT NULL DEFAULT 1 CHECK (severity BETWEEN 1 AND 5),
  attachment_path TEXT, action_taken TEXT, admin_comment TEXT,
  created_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%SZ','now'))
);
CREATE INDEX ix_discipline_student ON DisciplineRecords(student_id, event_date);

CREATE TABLE Messages (
  id INTEGER PRIMARY KEY,
  sender_user_id INTEGER NOT NULL REFERENCES Users(id),
  recipient_user_id INTEGER NOT NULL REFERENCES Users(id),
  body TEXT NOT NULL,
  sent_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%SZ','now')),
  read_at TEXT,
  CHECK (sender_user_id <> recipient_user_id)
);
CREATE INDEX ix_messages_conv ON Messages(recipient_user_id, read_at);
CREATE INDEX ix_messages_pair ON Messages(sender_user_id, recipient_user_id, sent_at);
CREATE TABLE MessageAttachments (
  id INTEGER PRIMARY KEY,
  message_id INTEGER NOT NULL REFERENCES Messages(id) ON DELETE CASCADE,
  original_name TEXT NOT NULL, stored_path TEXT NOT NULL,
  extension TEXT NOT NULL CHECK (extension IN ('pdf','docx','xlsx','jpg','jpeg','png','zip')),
  size_bytes INTEGER NOT NULL CHECK (size_bytes BETWEEN 1 AND 26214400),   -- 25 MB cap
  sha256 TEXT NOT NULL
);

CREATE TABLE Notifications (
  id INTEGER PRIMARY KEY,
  user_id INTEGER NOT NULL REFERENCES Users(id) ON DELETE CASCADE,
  category TEXT NOT NULL CHECK (category IN ('message','bell','teacher_attendance','student_absence','grade','discipline','backup','system')),
  title TEXT NOT NULL, body TEXT, ref_table TEXT, ref_id INTEGER,
  created_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%SZ','now')),
  read_at TEXT
);
CREATE INDEX ix_notif_user ON Notifications(user_id, read_at, created_at);

CREATE TABLE Backups (
  id INTEGER PRIMARY KEY,
  file_path TEXT NOT NULL, size_bytes INTEGER, sha256 TEXT,
  kind TEXT NOT NULL CHECK (kind IN ('auto','manual','daily','weekly','safety')),
  trigger_reason TEXT,
  status TEXT NOT NULL DEFAULT 'ok' CHECK (status IN ('ok','failed','verified')),
  created_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%SZ','now')),
  created_by_user_id INTEGER REFERENCES Users(id)
);
CREATE INDEX ix_backups_created ON Backups(created_at);

CREATE TABLE AuditLogs (
  id INTEGER PRIMARY KEY,
  user_id INTEGER REFERENCES Users(id),
  action TEXT NOT NULL, target_table TEXT, target_id INTEGER, details TEXT,
  ip_address TEXT, device_info TEXT,
  created_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%SZ','now'))
);
CREATE INDEX ix_audit_time ON AuditLogs(created_at);
CREATE INDEX ix_audit_user ON AuditLogs(user_id, created_at);

CREATE TABLE Settings (
  key TEXT PRIMARY KEY, value TEXT,
  updated_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%SZ','now'))
);

-- Defense in depth (the API enforces the same rules):
CREATE TRIGGER trg_attendance_requires_timetable
BEFORE INSERT ON Attendance
WHEN NEW.status <> 'absent' AND NOT EXISTS (
  SELECT 1 FROM Timetables t
  WHERE t.teacher_id = NEW.teacher_id
    AND t.academic_year_id = NEW.academic_year_id
    AND t.day_of_week = ((CAST(strftime('%w', NEW.attendance_date) AS INTEGER) + 1) % 7))
BEGIN SELECT RAISE(ABORT, 'NO_TIMETABLE_FOR_DAY'); END;

CREATE TRIGGER trg_audit_no_update BEFORE UPDATE ON AuditLogs
BEGIN SELECT RAISE(ABORT, 'AUDIT_LOG_IMMUTABLE'); END;
CREATE TRIGGER trg_audit_no_delete BEFORE DELETE ON AuditLogs
BEGIN SELECT RAISE(ABORT, 'AUDIT_LOG_IMMUTABLE'); END;

INSERT INTO Roles(id,name,is_system) VALUES (1,'SuperAdmin',1),(2,'Principal',1),(3,'Teacher',1),(4,'Staff',1);
INSERT INTO Settings(key,value) VALUES ('data_mode','production'),('backup.keep_count','30'),('backup.daily','1'),('backup.weekly','1'),('language','fa');
INSERT INTO SchemaMigrations(version,name) VALUES (1,'initial');
