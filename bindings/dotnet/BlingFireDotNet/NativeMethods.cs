// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;

namespace BlingfireDotNet;

public static partial class NativeMethods
{
#if IOS
    private const string BlingFireTokDllName = "__Internal";
#else
    private const string BlingFireTokDllName = "blingfiretokdll";
#endif

    [LibraryImport(BlingFireTokDllName)]
    public static partial int GetBlingFireTokVersion();

    [LibraryImport(BlingFireTokDllName, StringMarshalling = StringMarshalling.Utf8)]
    public static partial nint LoadModel(string pszLdbFileName);

    [LibraryImport(BlingFireTokDllName)]
    public static partial nint SetModel(ReadOnlySpan<byte> imgBytes, int modelByteCount);

    [LibraryImport(BlingFireTokDllName)]
    public static partial int FreeModel(nint model);

    [LibraryImport(BlingFireTokDllName)]
    public static partial int SetNoDummyPrefix(nint model, [MarshalAs(UnmanagedType.U1)] bool fNoDummyPrefix);

    [LibraryImport(BlingFireTokDllName)]
    public static partial int TextToSentences(ReadOnlySpan<byte> inUtf8Str, int inUtf8StrLen, Span<byte> outBuff, int maxBuffSize);

    [LibraryImport(BlingFireTokDllName)]
    public static partial int TextToWords(ReadOnlySpan<byte> inUtf8Str, int inUtf8StrLen, Span<byte> outBuff, int maxBuffSize);

    [LibraryImport(BlingFireTokDllName)]
    public static partial int TextToSentencesWithModel(ReadOnlySpan<byte> inUtf8Str, int inUtf8StrLen, Span<byte> outBuff, int maxBuffSize, nint model);

    [LibraryImport(BlingFireTokDllName)]
    public static partial int TextToWordsWithModel(ReadOnlySpan<byte> inUtf8Str, int inUtf8StrLen, Span<byte> outBuff, int maxBuffSize, nint model);

    [LibraryImport(BlingFireTokDllName)]
    public static partial int TextToSentencesWithOffsets(ReadOnlySpan<byte> inUtf8Str, int inUtf8StrLen, Span<byte> outBuff, Span<int> startOffsets, Span<int> endOffsets, int maxBuffSize);

    [LibraryImport(BlingFireTokDllName)]
    public static partial int TextToWordsWithOffsets(ReadOnlySpan<byte> inUtf8Str, int inUtf8StrLen, Span<byte> outBuff, Span<int> startOffsets, Span<int> endOffsets, int maxBuffSize);

    [LibraryImport(BlingFireTokDllName)]
    public static partial int TextToSentencesWithOffsetsWithModel(ReadOnlySpan<byte> inUtf8Str, int inUtf8StrLen, Span<byte> outBuff, Span<int> startOffsets, Span<int> endOffsets, int maxBuffSize, nint model);

    [LibraryImport(BlingFireTokDllName)]
    public static partial int TextToWordsWithOffsetsWithModel(ReadOnlySpan<byte> inUtf8Str, int inUtf8StrLen, Span<byte> outBuff, Span<int> startOffsets, Span<int> endOffsets, int maxBuffSize, nint model);

    [LibraryImport(BlingFireTokDllName)]
    public static partial int WordHyphenationWithModel(ReadOnlySpan<byte> inUtf8Str, int inUtf8StrLen, Span<byte> outBuff, int maxBuffSize, nint model, int utf32HyCode);

    [LibraryImport(BlingFireTokDllName)]
    public static partial int TextToIds(nint model, ReadOnlySpan<byte> inUtf8Str, int inUtf8StrLen, Span<int> tokenIds, int maxBuffSize, int unkId);

    [LibraryImport(BlingFireTokDllName)]
    public static partial int TextToIdsWithOffsets(nint model, ReadOnlySpan<byte> inUtf8Str, int inUtf8StrLen, Span<int> tokenIds, Span<int> startOffsets, Span<int> endOffsets, int maxBuffSize, int unkId);

    [LibraryImport(BlingFireTokDllName)]
    public static partial int NormalizeSpaces(ReadOnlySpan<byte> inUtf8Str, int inUtf8StrLen, Span<byte> outBuff, int maxBuffSize, int utf32SpaceCode);

    [LibraryImport(BlingFireTokDllName)]
    public static partial int TextToHashes(ReadOnlySpan<byte> inUtf8Str, int inUtf8StrLen, Span<int> hashArr, int maxHashArrLength, int wordNgrams, int bucketSize);

    [LibraryImport(BlingFireTokDllName)]
    public static partial int IdsToText(nint model, ReadOnlySpan<int> ids, int idsCount, Span<byte> outBuff, int maxBuffSize, [MarshalAs(UnmanagedType.U1)] bool skipSpecialTokens);
}
