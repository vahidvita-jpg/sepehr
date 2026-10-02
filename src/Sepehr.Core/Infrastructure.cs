using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Sepehr.Core
{
    public static class AppPaths
    {
        public static readonly string Root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Sepehr");
        public static string Data { get { return Path.Combine(Root, "data"); } }
        public static string Logs { get { return Path.Combine(Root, "logs"); } }
        public static string Backups { get { return Path.Combine(Root, "backups"); } }
        public static string Media { get { return Path.Combine(Root, "media"); } }
        public static string DbFile { get { return Path.Combine(Data, "sepehr.db"); } }

        public static void EnsureAll()
        {
            foreach (var p in new[] { Root, Data, Logs, Backups, Media }) Directory.CreateDirectory(p);
        }
    }

    public static class Log
    {
        static readonly object Gate = new object();
        public static void Info(string area, string msg) { Write("INFO", area, msg); }
        public static void Error(string area, Exception ex) { Write("ERROR", area, ex.ToString()); }
        static void Write(string level, string area, string msg)
        {
            try
            {
                lock (Gate)
                {
                    Directory.CreateDirectory(AppPaths.Logs);
                    var file = Path.Combine(AppPaths.Logs, "sepehr-" + DateTime.Now.ToString("yyyyMMdd") + ".log");
                    File.AppendAllText(file, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " [" + level + "] " + area + ": " + msg + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch { /* logging must never crash the app */ }
        }
    }

    public static class Db
    {
        public static SQLiteConnection Open()
        {
            var c = new SQLiteConnection("Data Source=" + AppPaths.DbFile + ";Version=3;Foreign Keys=True;Journal Mode=Wal;Busy Timeout=5000;");
            c.Open();
            return c;
        }

        static void Bind(SQLiteCommand cmd, object[] p)
        {
            for (int i = 0; i < p.Length; i++) cmd.Parameters.AddWithValue("@p" + i, p[i] ?? DBNull.Value);
        }

        public static int Exec(SQLiteConnection c, string sql, params object[] p)
        {
            using (var cmd = new SQLiteCommand(sql, c)) { Bind(cmd, p); return cmd.ExecuteNonQuery(); }
        }

        public static long Scalar(SQLiteConnection c, string sql, params object[] p)
        {
            using (var cmd = new SQLiteCommand(sql, c))
            {
                Bind(cmd, p);
                var o = cmd.ExecuteScalar();
                return (o == null || o == DBNull.Value) ? 0 : Convert.ToInt64(o);
            }
        }

        public static SQLiteDataReader Query(SQLiteConnection c, string sql, params object[] p)
        {
            var cmd = new SQLiteCommand(sql, c);
            Bind(cmd, p);
            return cmd.ExecuteReader();
        }

        public static long LastId(SQLiteConnection c) { return Scalar(c, "SELECT last_insert_rowid()"); }
        public static string NowUtc() { return DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"); }
    }

    public static class Migrator
    {
        public static void Run()
        {
            using (var c = Db.Open())
            {
                bool hasTable = Db.Scalar(c, "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='SchemaMigrations'") > 0;
                var applied = new HashSet<int>();
                if (hasTable)
                {
                    using (var r = Db.Query(c, "SELECT version FROM SchemaMigrations"))
                        while (r.Read()) applied.Add(Convert.ToInt32(r[0]));
                }
                var asm = typeof(Migrator).Assembly;
                var names = asm.GetManifestResourceNames()
                    .Where(n => n.StartsWith("migrations.") && n.EndsWith(".sql")).OrderBy(n => n).ToList();
                foreach (var name in names)
                {
                    int version = int.Parse(name.Substring("migrations.".Length, 4));
                    if (applied.Contains(version)) continue;
                    string sql;
                    using (var s = asm.GetManifestResourceStream(name))
                    using (var rd = new StreamReader(s, Encoding.UTF8)) sql = rd.ReadToEnd();
                    using (var tx = c.BeginTransaction())
                    {
                        Db.Exec(c, sql);
                        tx.Commit();
                    }
                    Log.Info("Migration", "applied " + name);
                }
            }
        }
    }

    public static class PasswordHasher
    {
        const int Iterations = 100000;

        public static string Hash(string password)
        {
            var salt = new byte[16];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(salt);
            return "pbkdf2$" + Iterations + "$" + Convert.ToBase64String(salt) + "$" + Convert.ToBase64String(Derive(password, salt, Iterations));
        }

        public static bool Verify(string password, string stored)
        {
            if (string.IsNullOrEmpty(stored)) return false;
            var parts = stored.Split('$');
            int iter;
            if (parts.Length != 4 || parts[0] != "pbkdf2" || !int.TryParse(parts[1], out iter)) return false;
            try
            {
                var expected = Convert.FromBase64String(parts[3]);
                var actual = Derive(password, Convert.FromBase64String(parts[2]), iter);
                int diff = expected.Length ^ actual.Length;
                for (int i = 0; i < expected.Length && i < actual.Length; i++) diff |= expected[i] ^ actual[i];
                return diff == 0;
            }
            catch (FormatException) { return false; }
        }

        static byte[] Derive(string pw, byte[] salt, int iter)
        {
            using (var d = new Rfc2898DeriveBytes(pw, salt, iter)) return d.GetBytes(32);
        }
    }
}
