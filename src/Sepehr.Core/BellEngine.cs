using System;
using System.Collections.Generic;
using System.Linq;

namespace Sepehr.Core
{
    public class BellRow
    {
        public int Id;
        public string Kind;      // class | break | prayer | opening | closing | custom
        public string Label;
        public int? PeriodNo;
        public TimeSpan Start, End;
        public bool Enabled;
    }

    public class BellStatus
    {
        public string Phase;        // none | before | running | gap | after
        public BellRow Current;
        public BellRow NextClass;
        public TimeSpan Remaining;
    }

    public static class BellEngine
    {
        public static BellStatus Compute(IList<BellRow> rows, TimeSpan now)
        {
            var st = new BellStatus { Phase = "none" };
            var spans = rows.Where(r => r.Enabled && r.End > r.Start).OrderBy(r => r.Start).ToList();
            if (spans.Count == 0) return st;
            st.Current = spans.FirstOrDefault(r => r.Start <= now && now < r.End);
            st.NextClass = spans.FirstOrDefault(r => r.Kind == "class" && r.Start > now);
            if (st.Current != null) { st.Phase = "running"; st.Remaining = st.Current.End - now; }
            else if (now < spans[0].Start) st.Phase = "before";
            else if (now >= spans[spans.Count - 1].End) st.Phase = "after";
            else st.Phase = "gap";
            return st;
        }

        public static List<BellRow> Load()
        {
            var list = new List<BellRow>();
            using (var c = Db.Open())
            using (var r = Db.Query(c, "SELECT id,kind,label,period_no,start_time,end_time,enabled FROM BellSchedules ORDER BY start_time"))
            {
                while (r.Read())
                {
                    list.Add(new BellRow
                    {
                        Id = Convert.ToInt32(r[0]), Kind = r.GetString(1), Label = r.GetString(2),
                        PeriodNo = r.IsDBNull(3) ? (int?)null : Convert.ToInt32(r[3]),
                        Start = TimeSpan.Parse(r.GetString(4)), End = TimeSpan.Parse(r.GetString(5)),
                        Enabled = Convert.ToInt32(r[6]) == 1
                    });
                }
            }
            return list;
        }
    }
}
