using System;
using System.Collections.Generic;
using hOps.web.Models;
using hOps.web.ViewModels;

namespace hOps.web.Utilities
{
    public static class UserAvatarHelper
    {
        public static UserAvatarViewModel BuildFromUser(ApplicationUser? user, string displayName, string size = "md")
        {
            return Build(user?.ProfilePhotoPath, user?.FirstName, user?.LastName, displayName, size);
        }

        public static UserAvatarViewModel Build(string? profilePhotoPath, string? firstName, string? lastName, string displayName, string size = "md")
        {
            var normalizedDisplayName = string.IsNullOrWhiteSpace(displayName)
                ? "Unknown User"
                : displayName.Trim();

            var normalizedSize = NormalizeSize(size);

            return new UserAvatarViewModel
            {
                ImageUrl = string.IsNullOrWhiteSpace(profilePhotoPath) ? null : profilePhotoPath,
                Initials = BuildInitials(firstName, lastName, normalizedDisplayName),
                Name = normalizedDisplayName,
                Size = normalizedSize
            };
        }

        private static string NormalizeSize(string? size)
        {
            var normalized = size?.Trim().ToLowerInvariant();

            return normalized switch
            {
                "sm" or "small" => "sm",
                "lg" or "large" => "lg",
                "xl" => "xl",
                _ => "md"
            };
        }

        private static string BuildInitials(string? firstName, string? lastName, string fallbackName)
        {
            var initials = new List<char>(capacity: 2);

            void TryAddInitial(string? value)
            {
                if (initials.Count >= 2)
                {
                    return;
                }

                if (!string.IsNullOrWhiteSpace(value))
                {
                    var trimmed = value.Trim();
                    initials.Add(char.ToUpperInvariant(trimmed[0]));
                }
            }

            TryAddInitial(firstName);
            TryAddInitial(lastName);

            if (initials.Count < 2)
            {
                var parts = (fallbackName ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    TryAddInitial(part);
                    if (initials.Count == 2)
                    {
                        break;
                    }
                }
            }

            if (initials.Count < 2)
            {
                foreach (var character in fallbackName ?? string.Empty)
                {
                    if (char.IsLetterOrDigit(character))
                    {
                        initials.Add(char.ToUpperInvariant(character));
                        if (initials.Count == 2)
                        {
                            break;
                        }
                    }
                }
            }

            if (initials.Count == 0)
            {
                return "UU";
            }

            if (initials.Count == 1)
            {
                return new string(new[] { initials[0], initials[0] });
            }

            return new string(new[] { initials[0], initials[1] });
        }
    }
}
