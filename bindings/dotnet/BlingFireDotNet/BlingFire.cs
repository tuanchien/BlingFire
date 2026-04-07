// Licensed under the MIT License.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace BlingfireDotNet;

/// <summary>
/// C# bindings for blingfiretokdll.
/// For full API descriptions see blingfiretokdll.h.
/// </summary>
public static class BlingFire
{
    private static int? version;

    /// <summary>Returns the blingfiretokdll version.</summary>
    public static int Version
    {
        get
        {
            version ??= NativeMethods.GetBlingFireTokVersion();
            return version.Value;
        }
    }

    /// <summary>
    /// Loads a model from disk and returns a handle to it. Free with <see cref="FreeModel"/>.
    /// Relative paths are resolved against <see cref="AppContext.BaseDirectory"/> first,
    /// making this robust across <c>dotnet publish</c>, single-file, and AOT scenarios.
    /// Returns zero if the file cannot be found.
    /// </summary>
    public static nint LoadModel(string path)
    {
        if (!Path.IsPathRooted(path))
        {
            string candidate = Path.Combine(AppContext.BaseDirectory, path);
            if (File.Exists(candidate))
                path = candidate;
        }
        return File.Exists(path) ? NativeMethods.LoadModel(path) : 0;
    }

    /// <summary>
    /// Loads a model from <paramref name="stream"/> and returns a handle to it. Free with <see cref="FreeModel"/>.
    /// Use this overload for Android assets, iOS bundles, and embedded resources — any scenario
    /// where a file-system path is unavailable.
    /// </summary>
    public static nint LoadModel(Stream stream)
    {
        using MemoryStream ms = new();
        stream.CopyTo(ms);
        var len = (int)ms.Length;
        return NativeMethods.SetModel(ms.GetBuffer().AsSpan(0, len), len);
    }

    /// <summary>Loads a model from a byte buffer and returns a handle to it. Free with <see cref="FreeModel"/>.</summary>
    public static nint SetModel(ReadOnlySpan<byte> imgBytes) => NativeMethods.SetModel(imgBytes, imgBytes.Length);

    /// <summary>Frees a model loaded with <see cref="LoadModel(string)"/>, <see cref="LoadModel(Stream)"/>, or <see cref="SetModel"/>.</summary>
    public static int FreeModel(nint model) => NativeMethods.FreeModel(model);

    /// <summary>Toggles the dummy-prefix behavior on a loaded model.</summary>
    public static int SetNoDummyPrefix(nint model, bool fNoDummyPrefix) => NativeMethods.SetNoDummyPrefix(model, fNoDummyPrefix);

    /// <summary>Splits <paramref name="paragraph"/> into sentences using the built-in model.</summary>
    public static IEnumerable<string> GetSentences(string paragraph) => SplitText(paragraph, '\n', (input, buf) => NativeMethods.TextToSentences(input, input.Length, buf, buf.Length));

    /// <summary>Tokenizes <paramref name="sentence"/> into words using the built-in model.</summary>
    public static IEnumerable<string> GetWords(string sentence) => SplitText(sentence, ' ', (input, buf) => NativeMethods.TextToWords(input, input.Length, buf, buf.Length));

    /// <summary>Splits <paramref name="paragraph"/> into sentences using <paramref name="model"/>.</summary>
    public static IEnumerable<string> GetSentencesWithModel(string paragraph, nint model) => SplitText(paragraph, '\n', (input, buf) => NativeMethods.TextToSentencesWithModel(input, input.Length, buf, buf.Length, model));

    /// <summary>Tokenizes <paramref name="sentence"/> into words using <paramref name="model"/>.</summary>
    public static IEnumerable<string> GetWordsWithModel(string sentence, nint model) => SplitText(sentence, ' ', (input, buf) => NativeMethods.TextToWordsWithModel(input, input.Length, buf, buf.Length, model));

    /// <inheritdoc cref="GetSentencesWithOffsets(byte[])"/>
    public static IEnumerable<(string Text, int Start, int End)> GetSentencesWithOffsets(string paragraph) => GetSentencesWithOffsets(Encoding.UTF8.GetBytes(paragraph));

    /// <summary>
    /// Splits <paramref name="paraBytes"/> (UTF-8) into sentences using the built-in model,
    /// returning each sentence with its byte offsets in the source buffer.
    /// </summary>
    public static IEnumerable<(string Text, int Start, int End)> GetSentencesWithOffsets(byte[] paraBytes) => SplitTextWithOffsets(paraBytes, '\n', (input, buf, starts, ends) => NativeMethods.TextToSentencesWithOffsets(input, input.Length, buf, starts, ends, buf.Length));

    /// <summary>
    /// Tokenizes <paramref name="sentence"/> into words using the built-in model,
    /// returning each word with its byte offsets in the source string.
    /// </summary>
    public static IEnumerable<(string Text, int Start, int End)> GetWordsWithOffsets(string sentence) => SplitTextWithOffsets(Encoding.UTF8.GetBytes(sentence), ' ', (input, buf, starts, ends) => NativeMethods.TextToWordsWithOffsets(input, input.Length, buf, starts, ends, buf.Length));

    /// <summary>Sentence split with offsets using <paramref name="model"/>.</summary>
    public static IEnumerable<(string Text, int Start, int End)> GetSentencesWithOffsetsWithModel(string paragraph, nint model) => SplitTextWithOffsets(Encoding.UTF8.GetBytes(paragraph), '\n', (input, buf, starts, ends) => NativeMethods.TextToSentencesWithOffsetsWithModel(input, input.Length, buf, starts, ends, buf.Length, model));

    /// <summary>Word tokenization with offsets using <paramref name="model"/>.</summary>
    public static IEnumerable<(string Text, int Start, int End)> GetWordsWithOffsetsWithModel(string sentence, nint model) => SplitTextWithOffsets(Encoding.UTF8.GetBytes(sentence), ' ', (input, buf, starts, ends) => NativeMethods.TextToWordsWithOffsetsWithModel(input, input.Length, buf, starts, ends, buf.Length, model));

    /// <summary>Returns only the byte offsets of sentence boundaries in <paramref name="paragraph"/>.</summary>
    public static IEnumerable<(int Start, int End)> GetSentenceBoundaries(string paragraph) => ExtractBoundaries(Encoding.UTF8.GetBytes(paragraph), (byte)'\n', (input, buf, starts, ends) => NativeMethods.TextToSentencesWithOffsets(input, input.Length, buf, starts, ends, buf.Length));

    /// <summary>Returns only the byte offsets of sentence boundaries using <paramref name="model"/>.</summary>
    public static IEnumerable<(int Start, int End)> GetSentenceBoundariesWithModel(string paragraph, nint model) => ExtractBoundaries(Encoding.UTF8.GetBytes(paragraph), (byte)'\n', (input, buf, starts, ends) => NativeMethods.TextToSentencesWithOffsetsWithModel(input, input.Length, buf, starts, ends, buf.Length, model));

    /// <summary>Converts <paramref name="text"/> to token IDs using <paramref name="model"/>.</summary>
    public static int[] TextToIds(nint model, string text, int maxLen, int unkId = 0)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        int[] ids = ArrayPool<int>.Shared.Rent(maxLen);

        try
        {
            int actual = NativeMethods.TextToIds(model, bytes, bytes.Length, ids, maxLen, unkId);
            if (actual <= 0)
                return Array.Empty<int>();

            int count = Math.Min(actual, maxLen);
            return Trim(ids, count);
        }
        finally
        {
            ArrayPool<int>.Shared.Return(ids);
        }
    }

    /// <summary>Converts <paramref name="text"/> to token IDs with offsets using <paramref name="model"/>.</summary>
    public static (int[] Ids, int[] Starts, int[] Ends) TextToIdsWithOffsets(nint model, string text, int maxLen, int unkId = 0)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        int[] ids = ArrayPool<int>.Shared.Rent(maxLen);
        int[] starts = ArrayPool<int>.Shared.Rent(maxLen);
        int[] ends = ArrayPool<int>.Shared.Rent(maxLen);

        try
        {
            int actual = NativeMethods.TextToIdsWithOffsets(model, bytes, bytes.Length, ids, starts.AsSpan(0, maxLen), ends.AsSpan(0, maxLen), maxLen, unkId);
            if (actual <= 0)
                return (Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>());

            int count = Math.Min(actual, maxLen);
            return (Trim(ids, count), Trim(starts, count), Trim(ends, count));
        }
        finally
        {
            ArrayPool<int>.Shared.Return(ids);
            ArrayPool<int>.Shared.Return(starts);
            ArrayPool<int>.Shared.Return(ends);
        }
    }

    /// <summary>Converts an array of token IDs back to text using <paramref name="model"/>.</summary>
    public static string IdsToText(nint model, ReadOnlySpan<int> ids, bool skipSpecialTokens = true)
    {
        if (ids.Length == 0)
            return string.Empty;

        int size = ids.Length * 16;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(size);

        try
        {
            int actual = NativeMethods.IdsToText(model, ids, ids.Length, buffer.AsSpan(0, size), size, skipSpecialTokens);

            if (actual > size)
            {
                ArrayPool<byte>.Shared.Return(buffer);
                size = actual;
                buffer = ArrayPool<byte>.Shared.Rent(size);
                actual = NativeMethods.IdsToText(model, ids, ids.Length, buffer.AsSpan(0, size), size, skipSpecialTokens);
            }

            return Encoding.UTF8.GetString(buffer, 0, actual);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>Normalizes whitespace in <paramref name="text"/>, replacing runs with <paramref name="utf32SpaceCode"/>.</summary>
    public static string NormalizeSpaces(string text, int utf32SpaceCode = 0x20)
    {
        var byteCount = Encoding.UTF8.GetByteCount(text);
        var size = (4 * byteCount) + 1;
        byte[] input = ArrayPool<byte>.Shared.Rent(byteCount);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(size);

        try
        {
            Encoding.UTF8.GetBytes(text, input);
            int actual = NativeMethods.NormalizeSpaces(input.AsSpan(0, byteCount), byteCount, buffer.AsSpan(0, size), size, utf32SpaceCode);
            if (actual > size)
                return string.Empty;
            return Encoding.UTF8.GetString(buffer, 0, actual);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(input);
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>Low-level wrapper over <c>WordHyphenationWithModel</c>.</summary>
    public static int WordHyphenationWithModel(ReadOnlySpan<byte> inUtf8Str, int inUtf8StrLen, Span<byte> outBuff, int maxBuffSize, nint model, int utf32HyCode = 0x2D) => NativeMethods.WordHyphenationWithModel(inUtf8Str, inUtf8StrLen, outBuff, maxBuffSize, model, utf32HyCode);

    /// <summary>Low-level wrapper over <c>TextToHashes</c>.</summary>
    public static int TextToHashes(ReadOnlySpan<byte> inUtf8Str, int inUtf8StrLen, Span<int> hashArr, int maxHashArrLength, int wordNgrams, int bucketSize = 2_000_000) => NativeMethods.TextToHashes(inUtf8Str, inUtf8StrLen, hashArr, maxHashArrLength, wordNgrams, bucketSize);

    private delegate int TokenizeFunc(ReadOnlySpan<byte> input, Span<byte> buf);
    private delegate int TokenizeWithOffsetsFunc(ReadOnlySpan<byte> input, Span<byte> buf, Span<int> starts, Span<int> ends);
    private delegate int BoundariesFunc(ReadOnlySpan<byte> input, Span<byte> buf, Span<int> starts, Span<int> ends);

    private static string[] SplitText(string text, char separator, TokenizeFunc tokenize)
    {
        int byteCount = Encoding.UTF8.GetByteCount(text);
        if (byteCount == 0)
            return Array.Empty<string>();

        int size = (2 * byteCount) + 1;
        byte[] input = ArrayPool<byte>.Shared.Rent(byteCount == 0 ? 1 : byteCount);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(size);
        try
        {
            Encoding.UTF8.GetBytes(text, input);

            int actual = tokenize(input.AsSpan(0, byteCount), buffer.AsSpan(0, size));
            if (actual > size)
            {
                ArrayPool<byte>.Shared.Return(buffer);
                size = actual;
                buffer = ArrayPool<byte>.Shared.Rent(size);
                actual = tokenize(input.AsSpan(0, byteCount), buffer.AsSpan(0, size));
            }
            if (actual <= 1 || actual > size)
                return Array.Empty<string>();

            return Encoding.UTF8.GetString(buffer, 0, actual - 1)
                .Split(separator, StringSplitOptions.RemoveEmptyEntries);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(input);
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static (string Text, int Start, int End)[] SplitTextWithOffsets(byte[] inputBytes, char separator, TokenizeWithOffsetsFunc tokenize)
    {
        int size = (2 * inputBytes.Length) + 1;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(size);
        int[] starts = ArrayPool<int>.Shared.Rent(size);
        int[] ends = ArrayPool<int>.Shared.Rent(size);

        try
        {
            var actual = tokenize(inputBytes, buffer.AsSpan(0, size), starts.AsSpan(0, size), ends.AsSpan(0, size));
            if (actual > size)
            {
                ArrayPool<byte>.Shared.Return(buffer);
                ArrayPool<int>.Shared.Return(starts);
                ArrayPool<int>.Shared.Return(ends);
                size = actual;
                buffer = ArrayPool<byte>.Shared.Rent(size);
                starts = ArrayPool<int>.Shared.Rent(size);
                ends = ArrayPool<int>.Shared.Rent(size);
                actual = tokenize(inputBytes, buffer.AsSpan(0, size), starts.AsSpan(0, size), ends.AsSpan(0, size));
            }
            if (actual <= 1 || actual > size)
                return Array.Empty<(string, int, int)>();

            return Encoding.UTF8.GetString(buffer, 0, actual - 1)
                .Split(separator, StringSplitOptions.RemoveEmptyEntries)
                .Select((str, i) => (str, starts[i], ends[i]))
                .ToArray();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            ArrayPool<int>.Shared.Return(starts);
            ArrayPool<int>.Shared.Return(ends);
        }
    }

    private static (int Start, int End)[] ExtractBoundaries(byte[] inputBytes, byte separator, BoundariesFunc tokenize)
    {
        int size = (2 * inputBytes.Length) + 1;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(size);
        int[] starts = ArrayPool<int>.Shared.Rent(size);
        int[] ends = ArrayPool<int>.Shared.Rent(size);

        try
        {
            int actual = tokenize(inputBytes, buffer.AsSpan(0, size), starts.AsSpan(0, size), ends.AsSpan(0, size));
            if (actual > size)
            {
                ArrayPool<byte>.Shared.Return(buffer);
                ArrayPool<int>.Shared.Return(starts);
                ArrayPool<int>.Shared.Return(ends);
                size = actual;
                buffer = ArrayPool<byte>.Shared.Rent(size);
                starts = ArrayPool<int>.Shared.Rent(size);
                ends = ArrayPool<int>.Shared.Rent(size);
                actual = tokenize(inputBytes, buffer.AsSpan(0, size), starts.AsSpan(0, size), ends.AsSpan(0, size));
            }
            if (actual <= 1 || actual > size)
                return Array.Empty<(int, int)>();

            int count = buffer.AsSpan(0, actual - 1).Count(separator) + 1;

            return Enumerable.Range(0, count)
                .Select(i => (starts[i], ends[i]))
                .ToArray();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            ArrayPool<int>.Shared.Return(starts);
            ArrayPool<int>.Shared.Return(ends);
        }
    }

    private static int[] Trim(ReadOnlySpan<int> source, int count)
    {
        var trimmed = new int[count];
        source.Slice(0, count).CopyTo(trimmed);
        return trimmed;
    }
}
