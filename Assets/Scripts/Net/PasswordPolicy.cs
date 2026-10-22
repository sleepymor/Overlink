namespace Overlink.Net
{
    /// <summary>
    /// Unity Authentication's username/password rules, stated in one place so a player who misses a
    /// requirement is told <i>which</i> one instead of reading a generic rejection.
    /// <para>
    /// The service owns the real policy and still has the final say; this exists because its answer
    /// does not. A weak password comes back as a <c>RequestFailedException</c> whose text is either
    /// empty or a sentence about "the password" in general, which <see cref="AuthResult.FromException"/>
    /// can only map to "That password does not meet the requirements." — a message that names nothing
    /// and therefore produces another guess. Checking the six rules locally turns that into one reply
    /// that lists exactly what is missing.
    /// </para>
    /// <para>
    /// The rules are transcribed from the SDK's own documentation
    /// (<c>com.unity.services.authentication/Runtime/IAuthenticationService.cs</c>, the
    /// <c>SignUpWithUsernamePasswordAsync</c> remarks): 8–30 characters with at least one uppercase
    /// letter, one lowercase letter, one digit and one special character. When Unity changes that text,
    /// this is the file to change with it; the server-side rejection stays as the backstop, so a stale
    /// copy here degrades into "the game explains one rule wrong", not "an insecure account is created".
    /// </para>
    /// </summary>
    public static class PasswordPolicy
    {
        public const int MinLength = 8;
        public const int MaxLength = 30;

        /// <summary>
        /// One line for the register form: every rule at once, so the player reads them before typing
        /// rather than after failing on one.
        /// </summary>
        public static string Requirements =>
            $"Use {MinLength}–{MaxLength} characters with an uppercase letter, a lowercase letter, " +
            "a number and a symbol.";

        /// <summary>
        /// Returns <see langword="true"/> when <paramref name="password"/> can be accepted, or
        /// <see langword="false"/> with the player-facing sentence naming everything it is missing.
        /// <para>
        /// All the failures are collected rather than returning on the first: a password short on
        /// length <i>and</i> symbols should say so once, because fixing one at a time turns a single
        /// correction into three round trips through the same form.
        /// </para>
        /// </summary>
        public static bool Validate(string password, out string error)
        {
            if (string.IsNullOrEmpty(password))
            {
                error = "Choose a password.";
                return false;
            }

            // A space would satisfy "a special character" under any loose reading of it, and Unity's
            // endpoint will not take one, so it is named here instead of being counted as a symbol.
            if (password.IndexOf(' ') >= 0)
            {
                error = "Your password cannot contain spaces.";
                return false;
            }

            bool tooShort = password.Length < MinLength;
            bool tooLong = password.Length > MaxLength;
            bool upper = false, lower = false, digit = false, symbol = false;
            foreach (char c in password)
            {
                if (char.IsUpper(c)) upper = true;
                else if (char.IsLower(c)) lower = true;
                else if (char.IsDigit(c)) digit = true;
                else if (!char.IsLetter(c)) symbol = true;
            }

            var missing = new System.Collections.Generic.List<string>(6);
            if (tooShort) missing.Add($"{MinLength} characters (it has {password.Length})");
            if (tooLong) missing.Add($"no more than {MaxLength} characters");
            if (!upper) missing.Add("an uppercase letter");
            if (!lower) missing.Add("a lowercase letter");
            if (!digit) missing.Add("a number");
            if (!symbol) missing.Add("a symbol");

            if (missing.Count == 0)
            {
                error = null;
                return true;
            }

            error = "Your password needs " + Join(missing) + ".";
            return false;
        }

        /// <summary>"a", "a and b", "a, b and c" — the list reads as prose, not as a delimiter dump.</summary>
        static string Join(System.Collections.Generic.IList<string> items)
        {
            if (items.Count == 1) return items[0];
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < items.Count - 1; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(items[i]);
            }
            sb.Append(" and ").Append(items[items.Count - 1]);
            return sb.ToString();
        }
    }
}
