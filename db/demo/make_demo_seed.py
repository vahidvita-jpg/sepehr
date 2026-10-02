"""Generates db/demo/demo_seed.sql: 1 school, 1 admin, 10 teachers, 3 grade levels,
6 classes, 10 subjects, 30 students, morning-shift bells, and a conflict-free timetable.
DEMO ONLY - never applied by the installer's production path.
The demo admin has an unusable password hash; real admins are created in the first-run wizard."""
import sys

def nid(seed):                       # valid Iranian national code
    d = [int(c) for c in f"{seed:09d}"]
    r = sum(d[i] * (10 - i) for i in range(9)) % 11
    return f"{seed:09d}{r if r < 2 else 11 - r}"

subjects = ["ریاضی","ادبیات فارسی","علوم تجربی","زبان انگلیسی","عربی","دین و زندگی","مطالعات اجتماعی","تربیت بدنی","هنر","کار و فناوری"]
tf = ["احمد","رضا","مریم","زهرا","علی","فاطمه","حسین","سارا","محمد","نرگس"]
tl = ["احمدی","رضایی","کریمی","حسینی","موسوی","جعفری","صادقی","نوری","قاسمی","اکبری"]
sf = ["امیر","آرین","پارسا","سینا","نیما","کیان"]
sl = ["محمدی","اسدی","رستمی","یوسفی","کاظمی"]
classes = [(7,'الف'),(7,'ب'),(8,'الف'),(8,'ب'),(9,'الف'),(9,'ب')]
q = lambda s: "'" + s.replace("'", "''") + "'"
o = []
o.append("PRAGMA foreign_keys=ON; BEGIN;")
o.append("INSERT INTO Schools(id,name,code,principal_name,is_demo) VALUES (1,'آموزشگاه میرزا کوچک‌خان','DEMO-001','مدیر نمونه',1);")
o.append("INSERT INTO AcademicYears(id,school_id,label,start_date,end_date,is_current) VALUES (1,1,'1405-1406','2026-09-23','2027-06-21',1);")
o.append("INSERT INTO Users(id,school_id,username,password_hash,role_id,display_name) VALUES (1,1,'demo_admin','!DEMO-DISABLED!',1,'مدیر نمونه');")
o.append("INSERT INTO Shifts(id,school_id,name) VALUES (1,1,'صبح');")
for i, s in enumerate(subjects, 1): o.append(f"INSERT INTO Subjects(id,school_id,name) VALUES ({i},1,{q(s)});")
for i in range(10):
    t = i + 1
    o.append(f"INSERT INTO Users(id,school_id,username,password_hash,role_id,display_name) VALUES ({t+1},1,'demo_t{t}','!DEMO-DISABLED!',3,{q(tf[i]+' '+tl[i])});")
    o.append(f"INSERT INTO Teachers(id,school_id,user_id,first_name,last_name,national_id,employee_id,mobile,primary_subject_id) VALUES ({t},1,{t+1},{q(tf[i])},{q(tl[i])},'{nid(100000000+i)}','E{1000+t}','0912000{1000+t:04d}',{t});")
    o.append(f"INSERT INTO TeacherSubjects VALUES ({t},{t});")
for c,(g,s) in enumerate(classes, 1):
    o.append(f"INSERT INTO Classes(id,school_id,academic_year_id,grade_level,section,room,shift_id) VALUES ({c},1,1,{g},{q(s)},'{c}',1);")
# bells: opening, 4 periods with breaks, closing
bells = [(None,'opening','زنگ شروع','07:30','07:30'),(1,'class','زنگ اول','07:30','08:15'),(None,'break','تنفس','08:15','08:30'),
         (2,'class','زنگ دوم','08:30','09:15'),(None,'break','تنفس','09:15','09:30'),(3,'class','زنگ سوم','09:30','10:15'),
         (None,'break','تنفس','10:15','10:30'),(4,'class','زنگ چهارم','10:30','11:15'),(None,'closing','زنگ پایان','11:15','11:15')]
for i,(p,k,l,a,b) in enumerate(bells, 1):
    o.append(f"INSERT INTO BellSchedules(id,shift_id,period_no,kind,label,start_time,end_time) VALUES ({i},1,{'NULL' if p is None else p},'{k}',{q(l)},'{a}','{b}');")
period_bell = {1:2, 2:4, 3:6, 4:8}
seen = set()
tid = 0
for ci in range(6):
    for day in range(5):
        for p in range(1, 5):
            t = (ci + day*3 + p*2) % 10 + 1
            tid += 1
            o.append(f"INSERT INTO Timetables(id,academic_year_id,class_id,teacher_id,subject_id,day_of_week,bell_schedule_id,room) VALUES ({tid},1,{ci+1},{t},{t},{day},{period_bell[p]},'{ci+1}');")
            if (t, ci+1) not in seen:
                seen.add((t, ci+1)); o.append(f"INSERT INTO TeacherClasses VALUES ({t},{ci+1},{t});")
n = 0
for ci in range(6):
    for k in range(5):
        n += 1
        o.append(f"INSERT INTO Students(id,school_id,student_code,national_id,first_name,last_name,father_name,class_id,parent_phone) VALUES ({n},1,'S{5000+n}','{nid(200000000+n)}',{q(sf[ci])},{q(sl[k])},'پدر نمونه','{ci+1}','0935000{n:04d}');")
o.append("UPDATE Settings SET value='demo' WHERE key='data_mode';")
o.append("COMMIT;")
open(sys.argv[1] if len(sys.argv) > 1 else "demo_seed.sql", "w", encoding="utf-8").write("\n".join(o) + "\n")
