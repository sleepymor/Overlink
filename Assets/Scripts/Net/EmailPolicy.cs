namespace Overlink.Net
{
    /// <summary>
    /// Shape rules for the email address a player gives us, so a typo is caught in front of the person
    /// who made it rather than surfacing weeks later as a bounce nobody sees.
    /// <para>
    /// This is deliberately <b>not</b> a full RFC 5322 parser. Syntax can only ever prove that a string
    /// looks like an address; it cannot prove the mailbox exists, and it certainly cannot prove the
    /// player owns it. The one thing that does that is a delivered message, and nothing in this project
    /// sends mail yet — see the verification note in <c>docs/AUTH_FLOW.md</c>. Every rule here is a
    /// cheap guard against the mistakes a person actually makes (no <c>@</c>, a stray space,
    /// <c>gmail.con</c>) and against values that would be rejected outright by any real mail server.
    /// </para>
    /// <para>
    /// The length limits are not invented: RFC 5321 §4.5.3.1.3 caps a reverse path at 256 octets
    /// <i>including</i> the angle brackets, which leaves 254 for the address, and §4.5.3.1.4 caps the
    /// local part at 64 octets and each domain label at 63. A longer value is not "unusual", it is
    /// undeliverable, and the player would only learn that after we told them we had their address.
    /// </para>
    /// <para>
    /// Unlike <see cref="PasswordPolicy"/>, this class exposes no <c>Requirements</c> sentence for the
    /// form to display. Password rules are not guessable — nobody would infer that Unity wants an
    /// uppercase letter and a symbol — so they have to be written down. Email shape is guessable, and
    /// printing "the part before the @ is limited to 64 characters" at someone who is typing their own
    /// address is noise. What that field does need is a line saying <i>why</i> we want it, which is
    /// player-facing copy and therefore lives in the UI, not here.
    /// </para>
    /// </summary>
    public static class EmailPolicy
    {
        /// <summary>RFC 5321 §4.5.3.1.3: 256 octets for the reverse path, minus the angle brackets.</summary>
        public const int MaxLength = 254;

        /// <summary>RFC 5321 §4.5.3.1.4: the longest local part any server must accept.</summary>
        public const int MaxLocalLength = 64;

        /// <summary>RFC 5321 §4.5.3.1.4: the longest single label of the domain.</summary>
        public const int MaxDomainLabelLength = 63;

        /// <summary>
        /// Returns <see langword="true"/> when <paramref name="email"/> is worth storing, or
        /// <see langword="false"/> with one sentence naming the specific thing wrong with it.
        /// <para>
        /// Unlike a password, an address has exactly one correct form, so this reports the first fault
        /// rather than collecting them all: telling someone about the missing <c>@</c> and the stray
        /// space at once makes one fix look like two problems.
        /// </para>
        /// </summary>
        public static bool Validate(string email, out string error)
        {
            string value = (email ?? "").Trim();

            if (value.Length == 0)
            {
                error = "Enter your email address.";
                return false;
            }

            if (value.Length > MaxLength)
            {
                error = $"That address is longer than {MaxLength} characters, which no mail server accepts.";
                return false;
            }

            // Only INTERIOR whitespace is a mistake. A trailing space from an autofill is not one, and
            // telling someone their address "has a space in it" when the field looks clean is a message
            // they cannot act on — Normalize trims the ends, so the ends are handled, not forgiven.
            for (int i = 0; i < value.Length; i++)
            {
                if (char.IsWhiteSpace(value[i]))
                {
                    error = "Your email address has a space in it.";
                    return false;
                }
            }

            // International addresses are refused rather than passed through. An internationalised
            // domain needs Punycode conversion, which a client has no business performing, and a
            // mailer handed raw UTF-8 in a domain label bounces it — the player would get a dead
            // address on file and no reason to suspect it. Revisit with a real mail provider that
            // says what it accepts.
            foreach (char c in value)
            {
                if (c > 127)
                {
                    error = "Use plain ASCII characters in your email address — accents and other " +
                            "scripts aren't supported yet.";
                    return false;
                }
            }

            int at = value.IndexOf('@');
            if (at < 0)
            {
                error = "Your email address needs an @ in it — like name@example.com.";
                return false;
            }
            if (value.IndexOf('@', at + 1) >= 0)
            {
                error = "Your email address has more than one @ in it.";
                return false;
            }

            string local = value.Substring(0, at);
            string domain = value.Substring(at + 1);

            if (local.Length == 0)
            {
                error = "Your email address is missing the part before the @ — like name@example.com.";
                return false;
            }
            if (domain.Length == 0)
            {
                error = "Your email address is missing the domain after the @ — like name@example.com.";
                return false;
            }
            if (local.Length > MaxLocalLength)
            {
                error = $"The part before the @ can be at most {MaxLocalLength} characters.";
                return false;
            }

            if (!IsLocalPartValid(local, out error)) return false;
            if (!IsDomainValid(domain, out error)) return false;

            error = null;
            return true;
        }

        /// <summary>
        /// The canonical form that gets stored: trimmed and lower case.
        /// <para>
        /// The domain is case-insensitive by specification. The local part technically is not, but
        /// every provider that matters treats it as such, and a value we only ever <i>display</i> or
        /// <i>send to</i> gains nothing from preserving two spellings of the same mailbox — least of
        /// all when a later duplicate check is run against it.
        /// </para>
        /// Call after <see cref="Validate"/>, which refuses the whitespace this trims.
        /// </summary>
        public static string Normalize(string email)
        {
            return (email ?? "").Trim().ToLowerInvariant();
        }

        /// <summary>
        /// RFC 5322 §3.2.3 <c>atext</c> plus dots, with the dot placements a server actually enforces:
        /// no leading, trailing or consecutive dots, because those are the forms that get rejected as
        /// <c>..</c> is not a typo a player can see, but <c>name..@x.com</c> is invisible.
        /// </summary>
        static bool IsLocalPartValid(string local, out string error)
        {
            if (local[0] == '.' || local[local.Length - 1] == '.')
            {
                error = "The part before the @ can't start or end with a dot.";
                return false;
            }
            if (local.IndexOf("..") >= 0)
            {
                error = "The part before the @ can't have two dots in a row.";
                return false;
            }
            for (int i = 0; i < local.Length; i++)
            {
                if (!IsAtext(local[i]) && local[i] != '.')
                {
                    error = $"\"{local[i]}\" isn't allowed in an email address.";
                    return false;
                }
            }
            error = null;
            return true;
        }

        /// <summary>
        /// Two or more dot-separated labels of letters, digits and inner hyphens.
        /// <para>
        /// Requiring a dot is a deliberate policy choice, not a syntactic necessity: it rejects
        /// <c>name@localhost</c> and <c>name@intranet</c>, which are valid shapes and undeliverable
        /// from outside a private network. It also rejects a bare-literal-IP domain, which no consumer
        /// mail provider uses. The cost is a rare legitimate address (a short hostname on a private
        /// TLD) — acceptable against the benefit of not storing addresses that can never receive a
        /// receipt.
        /// </para>
        /// </summary>
        static bool IsDomainValid(string domain, out string error)
        {
            if (domain[0] == '.' || domain[domain.Length - 1] == '.')
            {
                error = "The domain part of your email address can't start or end with a dot.";
                return false;
            }
            if (domain.IndexOf("..") >= 0)
            {
                error = "The domain part of your email address can't have two dots in a row.";
                return false;
            }

            string[] labels = domain.Split('.');
            if (labels.Length < 2)
            {
                error = "Your email address needs a full domain — like name@example.com, not name@example.";
                return false;
            }

            for (int i = 0; i < labels.Length; i++)
            {
                string label = labels[i];
                if (label.Length == 0)
                {
                    error = "Check the dots in the domain part of your email address.";
                    return false;
                }
                if (label.Length > MaxDomainLabelLength)
                {
                    error = $"Each part of the domain is limited to {MaxDomainLabelLength} characters.";
                    return false;
                }
                if (label[0] == '-' || label[label.Length - 1] == '-')
                {
                    error = "The parts of your domain can't start or end with a hyphen.";
                    return false;
                }
                for (int c = 0; c < label.Length; c++)
                {
                    char ch = label[c];
                    bool asciiLetter = (ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z');
                    if (!asciiLetter && !(ch >= '0' && ch <= '9') && ch != '-')
                    {
                        error = $"\"{ch}\" isn't allowed in the domain part of an email address.";
                        return false;
                    }
                }

                // The last label must be letters and at least two long: that rules out an IP address
                // written as a domain, and a one-character TLD does not exist in the root zone.
                if (i == labels.Length - 1)
                {
                    for (int c = 0; c < label.Length; c++)
                    {
                        if (!char.IsLetter(label[c]))
                        {
                            error = "The last part of your domain should be letters, like .com or .gg.";
                            return false;
                        }
                    }
                    if (label.Length < 2)
                    {
                        error = "The last part of your domain looks too short — did you mean .com?";
                        return false;
                    }
                }
            }

            error = null;
            return true;
        }

        /// <summary>RFC 5322 §3.2.3 atext — the characters legal in an unquoted local part.</summary>
        static bool IsAtext(char c)
        {
            if (c >= 'a' && c <= 'z') return true;
            if (c >= 'A' && c <= 'Z') return true;
            if (c >= '0' && c <= '9') return true;
            switch (c)
            {
                case '!': case '#': case '$': case '%': case '&': case '\'': case '*':
                case '+': case '-': case '/': case '=': case '?': case '^': case '_':
                case '`': case '{': case '|': case '}': case '~':
                    return true;
                default:
                    return false;
            }
        }
    }
}
