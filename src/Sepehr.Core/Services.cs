using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;

namespace Sepehr.Core
{
    public static class Audit
    {
        public static void Write(SQLiteConnection c, int? userId, string action, string table, long? id, string details)
        {
            Db.Exec(c, "INSERT INTO AuditLogs(user_id,action,target_table,target_id,details,device_info) VALUES (@p0,@p1,@p2,@p3,@p4,@p5)",
                userId, action, table, id, details, Environment.MachineName);
        }
    }

    public class AuthResult
    {
        public bool Success;
        public string Error;
        public int UserId;
        public int RoleId;
        public string DisplayName;
    }

    public static class SetupService
    {
        public static bool IsFirstRun()
        {
            using (var c = Db.Open()) return Db.Scalar(c, "SELECT COUNT(*) FROM Users") == 0;
        }

        public static string SuggestAcademicYear(DateTime now)
        {
            int y, m, d;
            PersianDate.ToJalali(now.Year, now.Month, now.Day, out y, out m, out d);
            int start = m >= 7 ? y : y - 1;
            return start + "-" + (start + 1);
        }

        /// <summary>Returns an error message in Persian, or null on success.</summary>
        public static string Create(string schoolName, string principal, string yearLabel, string username, string password, string displayName)
        {
            schoolName = (schoolName ?? "").Trim();
            username = (username ?? "").Trim();
            if (schoolName.Length == 0) return "نام مدرسه را وارد کنید.";
            if (username.Length < 3) return "نام کاربری باید حداقل ۳ نویسه باشد.";
            if (password == null || password.Length < 8) return "رمز عبور باید حداقل ۸ نویسه باشد.";
            int jy;
            var ylabel = PersianDate.NormalizeDigits((yearLabel ?? "").Trim());
            if (ylabel.Length < 4 || !int.TryParse(ylabel.Substring(0, 4), out jy)) return "سال تحصیلی را به شکل 1405-1406 وارد کنید.";
            var start = PersianDate.ToGregorian(jy, 7, 1);
            var end = PersianDate.ToGregorian(jy + 1, 3, 31);

            using (var c = Db.Open())
            using (var tx = c.BeginTransaction())
            {
                if (Db.Scalar(c, "SELECT COUNT(*) FROM Users") > 0) return "نصب اولیه قبلاً انجام شده است.";
                Db.Exec(c, "INSERT INTO Schools(name,principal_name) VALUES (@p0,@p1)", schoolName, principal);
                long schoolId = Db.LastId(c);
                Db.Exec(c, "INSERT INTO AcademicYears(school_id,label,start_date,end_date,is_current) VALUES (@p0,@p1,@p2,@p3,1)",
                    schoolId, ylabel, start.ToString("yyyy-MM-dd"), end.ToString("yyyy-MM-dd"));
                Db.Exec(c, "INSERT INTO Shifts(school_id,name) VALUES (@p0,'صبح')", schoolId);
                long shiftId = Db.LastId(c);
                // Default EDITABLE bell template; the administrator changes times in the bell settings.
                var t = new object[][] {
                    new object[]{ null, "opening", "زنگ شروع", "07:30", "07:30" },
                    new object[]{ 1, "class", "زنگ اول", "07:30", "08:15" },
                    new object[]{ null, "break", "تنفس", "08:15", "08:30" },
                    new object[]{ 2, "class", "زنگ دوم", "08:30", "09:15" },
                    new object[]{ null, "break", "تنفس", "09:15", "09:30" },
                    new object[]{ 3, "class", "زنگ سوم", "09:30", "10:15" },
                    new object[]{ null, "break", "تنفس", "10:15", "10:30" },
                    new object[]{ 4, "class", "زنگ چهارم", "10:30", "11:15" },
                    new object[]{ null, "closing", "زنگ پایان", "11:15", "11:15" } };
                foreach (var b in t)
                    Db.Exec(c, "INSERT INTO BellSchedules(shift_id,period_no,kind,label,start_time,end_time) VALUES (@p0,@p1,@p2,@p3,@p4,@p5)",
                        shiftId, b[0], b[1], b[2], b[3], b[4]);
                Db.Exec(c, "INSERT INTO Users(school_id,username,password_hash,role_id,display_name) VALUES (@p0,@p1,@p2,1,@p3)",
                    schoolId, username, PasswordHasher.Hash(password), string.IsNullOrWhiteSpace(displayName) ? username : displayName.Trim());
                long uid = Db.LastId(c);
                Audit.Write(c, (int)uid, "first_run_setup", "Schools", schoolId, null);
                tx.Commit();
            }
            Log.Info("Setup", "first-run setup completed");
            return null;
        }
    }

    public static class AuthService
    {
        const int MaxAttempts = 5;
        const string Generic = "نام کاربری یا رمز عبور نادرست است.";

        public static AuthResult Login(string username, string password)
        {
            username = (username ?? "").Trim();
            using (var c = Db.Open())
            {
                int id = 0, role = 0, failed = 0, active = 0; string hash = null, name = null, lockedUntil = null;
                bool found;
                using (var r = Db.Query(c, "SELECT id,password_hash,role_id,display_name,is_active,failed_attempts,locked_until FROM Users WHERE username=@p0", username))
                {
                    found = r.Read();
                    if (found)
                    {
                        id = Convert.ToInt32(r[0]); hash = r.GetString(1); role = Convert.ToInt32(r[2]); name = r.GetString(3);
                        active = Convert.ToInt32(r[4]); failed = Convert.ToInt32(r[5]); lockedUntil = r.IsDBNull(6) ? null : r.GetString(6);
                    }
                }
                if (!found) { Audit.Write(c, null, "login_failed", "Users", null, "unknown user"); return new AuthResult { Error = Generic }; }
                if (active != 1) return new AuthResult { Error = "این حساب غیرفعال است." };
                DateTime until;
                if (lockedUntil != null && DateTime.TryParse(lockedUntil, CultureInfo.InvariantCulture,
                        DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out until) && until > DateTime.UtcNow)
                    return new AuthResult { Error = "به دلیل تلاش‌های ناموفق، حساب چند دقیقه قفل است. بعداً دوباره تلاش کنید." };

                if (!PasswordHasher.Verify(password ?? "", hash))
                {
                    failed++;
                    if (failed >= MaxAttempts)
                    {
                        Db.Exec(c, "UPDATE Users SET failed_attempts=0, locked_until=@p0 WHERE id=@p1",
                            DateTime.UtcNow.AddMinutes(5).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"), id);
                    }
                    else Db.Exec(c, "UPDATE Users SET failed_attempts=@p0 WHERE id=@p1", failed, id);
                    Audit.Write(c, id, "login_failed", "Users", id, null);
                    return new AuthResult { Error = Generic };
                }
                Db.Exec(c, "UPDATE Users SET failed_attempts=0, locked_until=NULL, last_login_at=@p0 WHERE id=@p1", Db.NowUtc(), id);
                Audit.Write(c, id, "login", "Users", id, null);
                return new AuthResult { Success = true, UserId = id, RoleId = role, DisplayName = name };
            }
        }

        public static string SchoolName()
        {
            using (var c = Db.Open())
            using (var r = Db.Query(c, "SELECT name FROM Schools ORDER BY id LIMIT 1"))
                return r.Read() ? r.GetString(0) : "";
        }
    }

    public class TeacherRow
    {
        public long Id { get; set; }
        public string FullName { get; set; }
        public string NationalId { get; set; }
        public string EmployeeId { get; set; }
        public string Mobile { get; set; }
        public string Status { get; set; }
    }

    public static class TeacherService
    {
        public static List<TeacherRow> List()
        {
            var list = new List<TeacherRow>();
            using (var c = Db.Open())
            using (var r = Db.Query(c, "SELECT id,first_name,last_name,national_id,employee_id,mobile,status FROM Teachers ORDER BY last_name,first_name"))
                while (r.Read())
                    list.Add(new TeacherRow
                    {
                        Id = Convert.ToInt64(r[0]), FullName = r.GetString(1) + " " + r.GetString(2), NationalId = r.GetString(3),
                        EmployeeId = r.IsDBNull(4) ? "" : r.GetString(4), Mobile = r.IsDBNull(5) ? "" : r.GetString(5),
                        Status = r.GetString(6) == "active" ? "فعال" : (r.GetString(6) == "leave" ? "مرخصی" : "غیرفعال")
                    });
            return list;
        }

        /// <summary>Returns a Persian error message or null on success. Only SuperAdmin(1)/Principal(2) may add teachers.</summary>
        public static string Add(AuthResult user, string first, string last, string nationalId, string employeeId, string mobile)
        {
            if (user == null || user.RoleId > 2) return "شما اجازه‌ی افزودن معلم را ندارید.";
            first = (first ?? "").Trim(); last = (last ?? "").Trim();
            nationalId = PersianDate.NormalizeDigits((nationalId ?? "").Trim());
            mobile = PersianDate.NormalizeDigits((mobile ?? "").Trim());
            employeeId = PersianDate.NormalizeDigits((employeeId ?? "").Trim());
            if (first.Length == 0 || last.Length == 0) return "نام و نام خانوادگی را وارد کنید.";
            if (!Validators.IsValidNationalId(nationalId)) return "کد ملی نامعتبر است.";
            if (mobile.Length > 0 && !Validators.IsValidMobile(mobile)) return "شماره‌ی موبایل باید با 09 شروع شود و ۱۱ رقم باشد.";
            try
            {
                using (var c = Db.Open())
                using (var tx = c.BeginTransaction())
                {
                    long schoolId = Db.Scalar(c, "SELECT id FROM Schools ORDER BY id LIMIT 1");
                    Db.Exec(c, "INSERT INTO Teachers(school_id,first_name,last_name,national_id,employee_id,mobile) VALUES (@p0,@p1,@p2,@p3,@p4,@p5)",
                        schoolId, first, last, nationalId, employeeId.Length == 0 ? null : employeeId, mobile.Length == 0 ? null : mobile);
                    long id = Db.LastId(c);
                    Audit.Write(c, user.UserId, "teacher_add", "Teachers", id, first + " " + last);
                    tx.Commit();
                }
                return null;
            }
            catch (SQLiteException ex)
            {
                Log.Error("TeacherService.Add", ex);
                if (ex.Message.IndexOf("UNIQUE", StringComparison.OrdinalIgnoreCase) >= 0) return "کد ملی یا کد پرسنلی تکراری است.";
                return "ثبت معلم انجام نشد. جزئیات در فایل گزارش ذخیره شد.";
            }
        }
    }
}
