using System;

namespace Fourgate.Core.Data;

/// <summary>Raised when a data file is missing, malformed or holds an invalid value.</summary>
public sealed class DataLoadException : Exception
{
    public DataLoadException(string file, string field, string reason, Exception? inner = null)
        : base($"{file}: {field}: {reason}", inner)
    {
        File = file;
        Field = field;
        Reason = reason;
    }

    public string File { get; }

    public string Field { get; }

    public string Reason { get; }

    /// <summary>Throws when <paramref name="isValid"/> is false.</summary>
    public static void Require(bool isValid, string file, string field, string reason)
    {
        if (!isValid)
        {
            throw new DataLoadException(file, field, reason);
        }
    }
}
