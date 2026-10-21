using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Overlink.Net
{
    /// <summary>
    /// Represents a player's unique identity in Riot ID style: Username#Tag
    /// Username is the display name (3-16 chars), Tag is the unique identifier (3-5 chars).
    /// The combination must be globally unique.
    /// </summary>
    [Serializable]
    public struct PlayerIdentity
    {
        public string Username;
        public string Tag;

        public PlayerIdentity(string username, string tag)
        {
            Username = username;
            Tag = tag;
        }

        /// <summary>
        /// Full ID in format Username#Tag (e.g., "Tiramisu#MISU")
        /// </summary>
        public string FullId => $"{Username}#{Tag}";

        /// <summary>
        /// Display name without tag for UI (just "Tiramisu")
        /// </summary>
        public string DisplayName => Username;

        /// <summary>
        /// True when both halves are present and well formed. A default instance is never valid, which
        /// is what lets the UI distinguish "no identity yet" from a name that is merely odd.
        /// </summary>
        public bool IsValid => ValidateUsername(Username, out _) && ValidateTag(Tag, out _);

        /// <summary>
        /// Parse a full ID string into username and tag.
        /// Returns true if valid format.
        /// </summary>
        public static bool TryParse(string fullId, out PlayerIdentity identity)
        {
            identity = default;

            if (string.IsNullOrWhiteSpace(fullId))
                return false;

            string[] parts = fullId.Split('#');
            if (parts.Length != 2)
                return false;

            string username = parts[0].Trim();
            string tag = parts[1].Trim();

            if (!ValidateUsername(username, out _))
                return false;

            if (!ValidateTag(tag, out _))
                return false;

            identity = new PlayerIdentity(username, tag);
            return true;
        }

        /// <summary>
        /// Validate username format.
        /// Username: 3-16 characters, letters, numbers, spaces, underscore, dash.
        /// Cannot start or end with space.
        /// </summary>
        public static bool ValidateUsername(string username, out string error)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                error = "Username cannot be empty.";
                return false;
            }

            if (username.Length < 3)
            {
                error = "Username must be at least 3 characters.";
                return false;
            }

            if (username.Length > 16)
            {
                error = "Username must be 16 characters or less.";
                return false;
            }

            // Cannot start or end with space
            if (username.StartsWith(" ") || username.EndsWith(" "))
            {
                error = "Username cannot start or end with a space.";
                return false;
            }

            // Check valid characters: letters, numbers, spaces, underscore, dash
            foreach (char c in username)
            {
                if (!char.IsLetterOrDigit(c) && c != ' ' && c != '_' && c != '-')
                {
                    error = "Username can only contain letters, numbers, spaces, underscore, and dash.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        /// <summary>
        /// Validate tag format.
        /// Tag: 3-5 characters, letters and numbers only (case insensitive but stored as uppercase).
        /// </summary>
        public static bool ValidateTag(string tag, out string error)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                error = "Tag cannot be empty.";
                return false;
            }

            if (tag.Length < 3)
            {
                error = "Tag must be at least 3 characters.";
                return false;
            }

            if (tag.Length > 5)
            {
                error = "Tag must be 5 characters or less.";
                return false;
            }

            // Only letters and numbers
            foreach (char c in tag)
            {
                if (!char.IsLetterOrDigit(c))
                {
                    error = "Tag can only contain letters and numbers.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        /// <summary>
        /// Generate a random tag (3-5 chars, letters and numbers).
        /// </summary>
        public static string GenerateRandomTag(int length = 4)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            length = Mathf.Clamp(length, 3, 5);
            
            char[] tag = new char[length];
            for (int i = 0; i < length; i++)
            {
                tag[i] = chars[UnityEngine.Random.Range(0, chars.Length)];
            }
            
            return new string(tag);
        }

        public override string ToString() => FullId;

        public override bool Equals(object obj)
        {
            if (obj is PlayerIdentity other)
            {
                return string.Equals(Username, other.Username, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(Tag, other.Tag, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return (Username?.ToLowerInvariant().GetHashCode() ?? 0) 
                 ^ (Tag?.ToLowerInvariant().GetHashCode() ?? 0);
        }
    }
}
