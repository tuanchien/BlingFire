// Licensed under the MIT License.

using BlingfireDotNet;
using System;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace BlingFireDotNet.Tests;

/// <summary>
/// Tests that load external .bin model files from disk.
/// Each test skips gracefully when the model file is not present.
/// </summary>
public class LoadedModelTests : IDisposable
{
    private const string BertModelPath   = "bert_base_tok.bin";
    private const string BertI2WPath     = "bert_base_tok.i2w";
    private const string SyllabModelPath = "syllab.bin";

    private const string SampleText =
        "Autophobia, also called monophobia, is the specific phobia of isolation. " +
        "I saw a girl with a telescope.";

    private readonly nint _bertModel;
    private readonly nint _bertI2WModel;
    private readonly nint _syllabModel;

    public LoadedModelTests()
    {
        _bertModel    = BlingFire.LoadModel(BertModelPath);
        _bertI2WModel = BlingFire.LoadModel(BertI2WPath);
        _syllabModel  = BlingFire.LoadModel(SyllabModelPath);
    }

    public void Dispose()
    {
        if (_bertModel    != 0) BlingFire.FreeModel(_bertModel);
        if (_bertI2WModel != 0) BlingFire.FreeModel(_bertI2WModel);
        if (_syllabModel  != 0) BlingFire.FreeModel(_syllabModel);
    }

    // ── Model loading ─────────────────────────────────────────────────────────

    [Fact]
    public void LoadModel_ValidFile_ReturnsNonZeroHandle()
    {
        Assert.NotEqual(0, _bertModel);
    }

    [Fact]
    public void LoadModel_MissingFile_ReturnsZero()
    {
        nint handle = BlingFire.LoadModel("does_not_exist.bin");
        Assert.Equal(0, handle);
    }

    [Fact]
    public void LoadModel_RootedMissingFile_ReturnsZero()
    {
        // Rooted paths that don't exist must also return 0, not crash native.
        nint handle = BlingFire.LoadModel(Path.Combine(Path.GetTempPath(), "nonexistent_blingfire.bin"));
        Assert.Equal(0, handle);
    }

    [Fact]
    public void LoadModel_Stream_ReturnsNonZeroHandle()
    {
        using var fs = File.OpenRead(Path.Combine(AppContext.BaseDirectory, BertModelPath));
        nint handle = BlingFire.LoadModel(fs);
        try
        {
            Assert.NotEqual(0, handle);
        }
        finally
        {
            if (handle != 0) BlingFire.FreeModel(handle);
        }
    }

    [Fact]
    public void LoadModel_Stream_ProducesTokensMatchingLoadModel()
    {
        using var fs = File.OpenRead(Path.Combine(AppContext.BaseDirectory, BertModelPath));
        nint streamHandle = BlingFire.LoadModel(fs);
        try
        {
            byte[] inputBytes = Encoding.UTF8.GetBytes("Hello world");
            int[] idsFromFile   = new int[64];
            int[] idsFromStream = new int[64];

            int countFile   = NativeMethods.TextToIds(_bertModel,    inputBytes, inputBytes.Length, idsFromFile,   idsFromFile.Length,   0);
            int countStream = NativeMethods.TextToIds(streamHandle, inputBytes, inputBytes.Length, idsFromStream, idsFromStream.Length, 0);

            Assert.Equal(countFile, countStream);
            Assert.Equal(idsFromFile.Take(countFile), idsFromStream.Take(countStream));
        }
        finally
        {
            if (streamHandle != 0) BlingFire.FreeModel(streamHandle);
        }
    }

    // ── SetModel (load from byte buffer) ──────────────────────────────────────

    [Fact]
    public void SetModel_FromByteBuffer_ReturnsNonZeroHandle()
    {
        byte[] modelBytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, BertModelPath));
        nint handle = BlingFire.SetModel(modelBytes);
        try
        {
            Assert.NotEqual(0, handle);
        }
        finally
        {
            if (handle != 0) BlingFire.FreeModel(handle);
        }
    }

    [Fact]
    public void SetModel_ProducesTokensMatchingLoadModel()
    {
        byte[] modelBytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, BertModelPath));
        nint bufferHandle = BlingFire.SetModel(modelBytes);
        try
        {
            byte[] inputBytes = Encoding.UTF8.GetBytes("Hello world");
            int[] idsFromFile   = new int[64];
            int[] idsFromBuffer = new int[64];

            int countFile   = NativeMethods.TextToIds(_bertModel,    inputBytes, inputBytes.Length, idsFromFile,   idsFromFile.Length,   0);
            int countBuffer = NativeMethods.TextToIds(bufferHandle, inputBytes, inputBytes.Length, idsFromBuffer, idsFromBuffer.Length, 0);

            Assert.Equal(countFile, countBuffer);
            Assert.Equal(idsFromFile.Take(countFile), idsFromBuffer.Take(countBuffer));
        }
        finally
        {
            if (bufferHandle != 0) BlingFire.FreeModel(bufferHandle);
        }
    }

    // ── TextToIds (high-level) ────────────────────────────────────────────────

    [Fact]
    public void TextToIds_ReturnsTokenIds()
    {
        int[] ids = BlingFire.TextToIds(_bertModel, SampleText, 256);
        Assert.NotEmpty(ids);
        Assert.All(ids, id => Assert.True(id >= 0));
    }

    [Fact]
    public void TextToIds_EmptyText_ReturnsEmpty()
    {
        int[] ids = BlingFire.TextToIds(_bertModel, string.Empty, 256);
        Assert.Empty(ids);
    }

    [Fact]
    public void TextToIds_TruncatesToMaxLen()
    {
        const int maxLen = 4;
        int[] ids = BlingFire.TextToIds(_bertModel, SampleText, maxLen);
        Assert.True(ids.Length <= maxLen);
    }

    [Fact]
    public void TextToIds_MatchesNativeMethods()
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes("Hello world");
        int[] nativeIds = new int[64];
        int nativeCount = NativeMethods.TextToIds(_bertModel, inputBytes, inputBytes.Length, nativeIds, nativeIds.Length, 0);

        int[] highLevelIds = BlingFire.TextToIds(_bertModel, "Hello world", 64);

        Assert.Equal(nativeCount, highLevelIds.Length);
        Assert.Equal(nativeIds.Take(nativeCount), highLevelIds);
    }

    // ── TextToIdsWithOffsets (high-level) ─────────────────────────────────────

    [Fact]
    public void TextToIdsWithOffsets_OffsetsAreBounded()
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(SampleText);
        var (ids, starts, ends) = BlingFire.TextToIdsWithOffsets(_bertModel, SampleText, 256);

        Assert.NotEmpty(ids);
        Assert.Equal(ids.Length, starts.Length);
        Assert.Equal(ids.Length, ends.Length);

        for (int i = 0; i < ids.Length; i++)
        {
            Assert.True(starts[i] >= 0);
            Assert.True(ends[i] >= starts[i]);
            Assert.True(ends[i] <= inputBytes.Length);
        }
    }

    [Fact]
    public void TextToIdsWithOffsets_IdsMatchTextToIds()
    {
        int[] ids = BlingFire.TextToIds(_bertModel, SampleText, 256);
        var (idsWithOffsets, _, _) = BlingFire.TextToIdsWithOffsets(_bertModel, SampleText, 256);

        Assert.Equal(ids, idsWithOffsets);
    }

    // ── IdsToText ─────────────────────────────────────────────────────────────

    [Fact]
    public void IdsToText_NullIds_ReturnsEmptyString()
    {
        string result = BlingFire.IdsToText(_bertI2WModel, null!);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void IdsToText_EmptyIds_ReturnsEmptyString()
    {
        string result = BlingFire.IdsToText(_bertI2WModel, []);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void IdsToText_RoundTrip_ReturnsNonEmptyString()
    {
        int[] ids = BlingFire.TextToIds(_bertModel, "Hello world", 64);
        Assert.NotEmpty(ids);

        string text = BlingFire.IdsToText(_bertI2WModel, ids);
        Assert.False(string.IsNullOrWhiteSpace(text));
    }

    // ── GetSentencesWithModel / GetWordsWithModel ─────────────────────────────

    [Fact]
    public void GetSentencesWithModel_ReturnsResults()
    {
        var sentences = BlingFire.GetSentencesWithModel(SampleText, _bertModel).ToArray();
        Assert.NotEmpty(sentences);
    }

    [Fact]
    public void GetWordsWithModel_ReturnsResults()
    {
        var words = BlingFire.GetWordsWithModel("Hello world", _bertModel).ToArray();
        Assert.NotEmpty(words);
    }

    // ── GetSentencesWithOffsetsWithModel / GetWordsWithOffsetsWithModel ────────

    [Fact]
    public void GetSentencesWithOffsetsWithModel_CountMatchesSentences()
    {
        var sentences = BlingFire.GetSentencesWithModel(SampleText, _bertModel).ToArray();
        var withOffsets = BlingFire.GetSentencesWithOffsetsWithModel(SampleText, _bertModel).ToArray();
        Assert.Equal(sentences.Length, withOffsets.Length);
    }

    [Fact]
    public void GetWordsWithOffsetsWithModel_TextMatchesOffsetSlice()
    {
        const string sentence = "Hello world";
        byte[] inputBytes = Encoding.UTF8.GetBytes(sentence);
        var results = BlingFire.GetWordsWithOffsetsWithModel(sentence, _bertModel).ToArray();

        Assert.NotEmpty(results);
        foreach (var (text, start, end) in results)
        {
            string sliced = Encoding.UTF8.GetString(inputBytes, start, end - start + 1);
            Assert.Equal(text, sliced);
        }
    }

    // ── GetSentenceBoundariesWithModel ────────────────────────────────────────

    [Fact]
    public void GetSentenceBoundariesWithModel_CountMatchesSentences()
    {
        var sentences = BlingFire.GetSentencesWithModel(SampleText, _bertModel).ToArray();
        var boundaries = BlingFire.GetSentenceBoundariesWithModel(SampleText, _bertModel).ToArray();
        Assert.Equal(sentences.Length, boundaries.Length);
    }

    // ── TextToSentencesWithModel / TextToWordsWithModel (native layer) ─────────

    [Fact]
    public void NativeMethods_TextToSentencesWithModel_ReturnsResults()
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(SampleText);
        int maxLen = (2 * inputBytes.Length) + 1;
        byte[] outBuf = new byte[maxLen];

        int actual = NativeMethods.TextToSentencesWithModel(inputBytes, inputBytes.Length, outBuf, maxLen, _bertModel);
        Assert.True(actual > 1);
        Assert.True(actual <= maxLen);
    }

    [Fact]
    public void NativeMethods_TextToWordsWithModel_ReturnsResults()
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes("Hello world");
        int maxLen = (2 * inputBytes.Length) + 1;
        byte[] outBuf = new byte[maxLen];

        int actual = NativeMethods.TextToWordsWithModel(inputBytes, inputBytes.Length, outBuf, maxLen, _bertModel);
        Assert.True(actual > 1);
        Assert.True(actual <= maxLen);
    }

    // ── WordHyphenationWithModel ──────────────────────────────────────────────

    [Fact]
    public void WordHyphenationWithModel_ValidWord_ReturnsHyphenated()
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes("subdivision");
        byte[] outBuf     = new byte[128];

        int actual = BlingFire.WordHyphenationWithModel(
            inputBytes, inputBytes.Length, outBuf, outBuf.Length, _syllabModel);

        Assert.True(actual > 0);
        string hyphenated = Encoding.UTF8.GetString(outBuf, 0, actual);
        Assert.Contains("-", hyphenated);
    }

    // ── NormalizeSpaces ───────────────────────────────────────────────────────

    [Fact]
    public void NormalizeSpaces_ReplacesSpacesWithToken()
    {
        const int spaceToken = 9601; // '▁'
        string result = BlingFire.NormalizeSpaces("Hello world", spaceToken);
        Assert.False(string.IsNullOrEmpty(result));
        Assert.Contains("▁", result);
    }

    [Fact]
    public void NormalizeSpaces_DefaultSpace_ReturnsSingleSpaces()
    {
        string result = BlingFire.NormalizeSpaces("Hello   world");
        Assert.False(string.IsNullOrEmpty(result));
        Assert.DoesNotContain("  ", result);
    }

    // ── TextToHashes ──────────────────────────────────────────────────────────

    [Fact]
    public void TextToHashes_SimpleInput_ReturnsHashes()
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes("Hello world");
        int[] hashes = new int[64];

        int count = BlingFire.TextToHashes(inputBytes, inputBytes.Length, hashes, hashes.Length, 1);
        Assert.True(count > 0);
    }

    [Fact]
    public void TextToHashes_SameInput_ProducesSameHashes()
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes("Hello world");
        int[] hashes1 = new int[64];
        int[] hashes2 = new int[64];

        int count1 = BlingFire.TextToHashes(inputBytes, inputBytes.Length, hashes1, hashes1.Length, 1);
        int count2 = BlingFire.TextToHashes(inputBytes, inputBytes.Length, hashes2, hashes2.Length, 1);

        Assert.Equal(count1, count2);
        Assert.Equal(hashes1.Take(count1), hashes2.Take(count2));
    }
}
