namespace ServiceLib.Models.Dto;

public class SemanticVersion
{
    private readonly int major;
    private readonly int minor;
    private readonly int patch;
    private readonly int revision;
    private readonly string version = "0.0.0";

    public SemanticVersion(int major, int minor, int patch)
    {
        this.major = major;
        this.minor = minor;
        this.patch = patch;
        revision = 0;
        version = $"{major}.{minor}.{patch}";
    }

    public SemanticVersion(string? value)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            version = value.Trim().RemovePrefix('v');
            var match = Regex.Match(
                version,
                @"^(?<major>\d+)\.(?<minor>\d+)(?:\.(?<patch>\d+))?(?:(?:-custom\.|\.)(?<revision>\d+))?(?:[-+].*)?$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                throw new ArgumentException("Invalid version string");
            }

            major = int.Parse(match.Groups["major"].Value);
            minor = int.Parse(match.Groups["minor"].Value);
            patch = match.Groups["patch"].Success ? int.Parse(match.Groups["patch"].Value) : 0;
            revision = match.Groups["revision"].Success ? int.Parse(match.Groups["revision"].Value) : 0;
        }
        catch
        {
            major = 0;
            minor = 0;
            patch = 0;
            revision = 0;
        }
    }

    public override bool Equals(object? obj)
    {
        if (obj is SemanticVersion other)
        {
            return major == other.major
                && minor == other.minor
                && patch == other.patch
                && revision == other.revision;
        }
        else
        {
            return false;
        }
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(major, minor, patch, revision);
    }

    /// <summary>
    /// Use ToVersionString(string? prefix) instead if possible.
    /// </summary>
    /// <returns>major.minor.patch</returns>
    public override string ToString()
    {
        return version;
    }

    public string ToVersionString(string? prefix = null)
    {
        if (prefix == null)
        {
            return version;
        }
        else
        {
            return $"{prefix}{version}";
        }
    }

    public static bool operator ==(SemanticVersion v1, SemanticVersion v2)
    { return v1.Equals(v2); }

    public static bool operator !=(SemanticVersion v1, SemanticVersion v2)
    { return !v1.Equals(v2); }

    public static bool operator >=(SemanticVersion v1, SemanticVersion v2)
    { return v1.GreaterEquals(v2); }

    public static bool operator <=(SemanticVersion v1, SemanticVersion v2)
    { return v1.LessEquals(v2); }

    #region Private

    private bool GreaterEquals(SemanticVersion other)
    {
        if (major < other.major)
        {
            return false;
        }
        else if (major > other.major)
        {
            return true;
        }
        else
        {
            if (minor < other.minor)
            {
                return false;
            }
            else if (minor > other.minor)
            {
                return true;
            }
            else
            {
                if (patch < other.patch)
                {
                    return false;
                }
                else if (patch > other.patch)
                {
                    return true;
                }
                return revision >= other.revision;
            }
        }
    }

    private bool LessEquals(SemanticVersion other)
    {
        if (major < other.major)
        {
            return true;
        }
        else if (major > other.major)
        {
            return false;
        }
        else
        {
            if (minor < other.minor)
            {
                return true;
            }
            else if (minor > other.minor)
            {
                return false;
            }
            else
            {
                if (patch < other.patch)
                {
                    return true;
                }
                else if (patch > other.patch)
                {
                    return false;
                }
                return revision <= other.revision;
            }
        }
    }

    #endregion Private
}
