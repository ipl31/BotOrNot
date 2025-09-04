using NUnit.Framework;

namespace BotOrNot;

[TestFixture]
public class TestReplayFile
{
    private string _tempDir;
    private string _tempFile;
    private string _tempFilePath;
    private string _tempFileWrongExtension;
    private string _tempFilePathWrongExtension;

    [SetUp]
    public void Setup()
    {
        _tempDir = Path.GetTempPath();
        _tempFile = $"replay_{Guid.NewGuid():N}.replay";
        _tempFilePath = Path.Combine(_tempDir, _tempFile);
        
        _tempFileWrongExtension = $"replay_{Guid.NewGuid():N}.not_correct_extension";
        _tempFilePathWrongExtension = Path.Combine(_tempDir, _tempFileWrongExtension);
        
    }

    [TearDown]
    public void TearDown()
    {
        if (!File.Exists(_tempFilePath))
        {
            return;
        }
        try
        {
            File.Delete(_tempFilePath);
        }
        catch (IOException)
        {
            Console.WriteLine("Failed to delete temporary file, must not exist.");
        }
        
        if (!File.Exists(_tempFilePathWrongExtension))
        {
            return;
        }
        try
        {
            File.Delete(_tempFilePathWrongExtension);
        }
        catch (IOException)
        {
            Console.WriteLine("Failed to delete temporary file, must not exist.");
        }
        
    }

    [Test]
    public void TestFileDoesNotExit()
    {
        Assert.That(File.Exists(_tempFilePath), Is.False);
        var replayFile = new ReplayFile(_tempFilePath);
        Assert.Throws<FileNotFoundException>(replayFile.Validate);
    }

    [Test]
    public void TestWrongExtension()
    {
        using (File.Create(_tempFilePathWrongExtension))
        {
            Assert.That(File.Exists(_tempFilePathWrongExtension), Is.True);
        }
        var replayFile = new ReplayFile(_tempFilePathWrongExtension);
        Assert.Throws<ArgumentException>(replayFile.Validate);
    }
}