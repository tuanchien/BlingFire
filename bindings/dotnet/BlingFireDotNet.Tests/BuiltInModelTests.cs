// Licensed under the MIT License.

using BlingfireDotNet;
using System.Linq;
using System.Text;
using Xunit;

namespace BlingFireDotNet.Tests;

/// <summary>
/// Tests for methods that use the library's built-in sentence / word models,
/// i.e. those that do not require a caller-supplied model file.
/// </summary>
public class BuiltInModelTests
{
    private const string MultiSentenceText =
        "Autophobia, also called monophobia, is the specific phobia of isolation. " +
        "I saw a girl with a telescope. " +
        "Я увидел девушку с телескопом.";

    // ── Version ───────────────────────────────────────────────────────────────

    [Fact]
    public void Version_ReturnsPositiveValue()
    {
        Assert.True(BlingFire.Version > 0);
    }

    [Fact]
    public void Version_IsCached()
    {
        // Second access must return the same value without re-querying native.
        Assert.Equal(BlingFire.Version, BlingFire.Version);
    }

    // ── GetSentences ──────────────────────────────────────────────────────────

    [Fact]
    public void GetSentences_MultiSentenceInput_ReturnsExpectedCount()
    {
        string[] sentences = BlingFire.GetSentences(MultiSentenceText).ToArray();
        Assert.Equal(3, sentences.Length);
    }

    [Fact]
    public void GetSentences_SingleSentence_ReturnsThatSentence()
    {
        const string input = "Hello world.";
        string[] sentences = BlingFire.GetSentences(input).ToArray();
        Assert.Single(sentences);
        Assert.Equal(input, sentences[0]);
    }

    [Fact]
    public void GetSentences_EmptyInput_ReturnsNoSentences()
    {
        string[] sentences = BlingFire.GetSentences(string.Empty).ToArray();
        Assert.Empty(sentences);
    }

    // ── GetSentencesWithOffsets ───────────────────────────────────────────────

    [Fact]
    public void GetSentencesWithOffsets_ReturnsOffsetsBoundedByInput()
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(MultiSentenceText);
        var results = BlingFire.GetSentencesWithOffsets(MultiSentenceText).ToArray();

        Assert.NotEmpty(results);
        foreach (var (_, start, end) in results)
        {
            Assert.True(start >= 0);
            Assert.True(end > start);
            Assert.True(end <= inputBytes.Length);
        }
    }

    [Fact]
    public void GetSentencesWithOffsets_CountMatchesGetSentences()
    {
        var sentences = BlingFire.GetSentences(MultiSentenceText).ToArray();
        var withOffsets = BlingFire.GetSentencesWithOffsets(MultiSentenceText).ToArray();
        Assert.Equal(sentences.Length, withOffsets.Length);
    }

    [Fact]
    public void GetSentencesWithOffsets_TextMatchesOffsetSlice()
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(MultiSentenceText);
        var results = BlingFire.GetSentencesWithOffsets(MultiSentenceText).ToArray();

        foreach (var (text, start, end) in results)
        {
            string sliced = Encoding.UTF8.GetString(inputBytes, start, end - start + 1);
            Assert.Equal(text, sliced);
        }
    }

    [Fact]
    public void GetSentencesWithOffsets_ByteArrayOverload_MatchesStringOverload()
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(MultiSentenceText);
        var fromString = BlingFire.GetSentencesWithOffsets(MultiSentenceText).ToArray();
        var fromBytes = BlingFire.GetSentencesWithOffsets(inputBytes).ToArray();

        Assert.Equal(fromString.Length, fromBytes.Length);
        for (int i = 0; i < fromString.Length; i++)
        {
            Assert.Equal(fromString[i], fromBytes[i]);
        }
    }

    // ── GetSentenceBoundaries ─────────────────────────────────────────────────

    [Fact]
    public void GetSentenceBoundaries_CountMatchesGetSentences()
    {
        var sentences = BlingFire.GetSentences(MultiSentenceText).ToArray();
        var boundaries = BlingFire.GetSentenceBoundaries(MultiSentenceText).ToArray();
        Assert.Equal(sentences.Length, boundaries.Length);
    }

    [Fact]
    public void GetSentenceBoundaries_MatchesWithOffsetsOffsets()
    {
        var withOffsets = BlingFire.GetSentencesWithOffsets(MultiSentenceText).ToArray();
        var boundaries = BlingFire.GetSentenceBoundaries(MultiSentenceText).ToArray();

        Assert.Equal(withOffsets.Length, boundaries.Length);
        for (int i = 0; i < withOffsets.Length; i++)
        {
            Assert.Equal(withOffsets[i].Start, boundaries[i].Start);
            Assert.Equal(withOffsets[i].End, boundaries[i].End);
        }
    }

    // ── GetWords ──────────────────────────────────────────────────────────────

    [Fact]
    public void GetWords_SimpleInput_ReturnsWords()
    {
        string[] words = BlingFire.GetWords("Hello world").ToArray();
        Assert.Equal(2, words.Length);
        Assert.Contains("Hello", words);
        Assert.Contains("world", words);
    }

    [Fact]
    public void GetWords_EmptyInput_ReturnsNoWords()
    {
        string[] words = BlingFire.GetWords(string.Empty).ToArray();
        Assert.Empty(words);
    }

    [Fact]
    public void GetWords_PunctuationIsSeparated()
    {
        // BlingFire splits punctuation as a separate token.
        string[] words = BlingFire.GetWords("Hello, world!").ToArray();
        Assert.True(words.Length > 2);
    }

    // ── GetWordsWithOffsets ───────────────────────────────────────────────────

    [Fact]
    public void GetWordsWithOffsets_CountMatchesGetWords()
    {
        const string sentence = "Hello world";
        var words = BlingFire.GetWords(sentence).ToArray();
        var withOffsets = BlingFire.GetWordsWithOffsets(sentence).ToArray();
        Assert.Equal(words.Length, withOffsets.Length);
    }

    [Fact]
    public void GetWordsWithOffsets_ReturnsValidOffsets()
    {
        const string sentence = "Hello world";
        byte[] inputBytes = Encoding.UTF8.GetBytes(sentence);
        var results = BlingFire.GetWordsWithOffsets(sentence).ToArray();

        Assert.NotEmpty(results);
        foreach (var (_, start, end) in results)
        {
            Assert.True(start >= 0);
            Assert.True(end > start);
            Assert.True(end <= inputBytes.Length);
        }
    }

    [Fact]
    public void GetWordsWithOffsets_TextMatchesOffsetSlice()
    {
        const string sentence = "Hello world";
        byte[] inputBytes = Encoding.UTF8.GetBytes(sentence);
        var results = BlingFire.GetWordsWithOffsets(sentence).ToArray();

        foreach (var (text, start, end) in results)
        {
            string sliced = Encoding.UTF8.GetString(inputBytes, start, end - start + 1);
            Assert.Equal(text, sliced);
        }
    }
}
