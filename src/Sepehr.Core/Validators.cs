using System.Linq;
using System.Text.RegularExpressions;

namespace Sepehr.Core
{
    public static class Validators
    {
        public static bool IsValidNationalId(string id)
        {
            if (id == null || !Regex.IsMatch(id, @"^\d{10}$")) return false;
            if (id.Distinct().Count() == 1) return false;
            int sum = 0;
            for (int i = 0; i < 9; i++) sum += (id[i] - '0') * (10 - i);
            int r = sum % 11;
            int check = id[9] - '0';
            return r < 2 ? check == r : check == 11 - r;
        }

        public static bool IsValidMobile(string m) { return m != null && Regex.IsMatch(m, @"^09\d{9}$"); }
    }
}
