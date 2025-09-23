using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Generic;
using System.IO;
using Xunit;
using System.Linq;
using Xunit.Abstractions;
using Xunit.Sdk;
using ArchS.Data;
using ArchS.Data.ProfileManager;
using ArchS.Data.FileManager;
using ArchS.Data.AppServices;
using ArchS.Data.NotifierServices;
namespace Tests;

// TEST MAC :  dotnet test ArchS.sln --framework net8.0 --logger "console;verbosity=detailed" 

public class TestOrderer : ITestCaseOrderer
{
    public IEnumerable<TTestCase> OrderTestCases<TTestCase>(IEnumerable<TTestCase> testCases) where TTestCase : ITestCase
    {
        var order = new List<string> { "TestBackup", "UpdateKeepTrackFlag", "UpdateBackup", "TestMappingOfFiles", "TestReflectionProfileMetadataUpdate", "TestDeleteBackups"};
        return testCases.OrderBy(tc => order.IndexOf(tc.TestMethod.Method.Name));
    }
}

public class BackUpControllerFixture : IDisposable
{
    public BackupService Controller { get; private set; }
    public readonly string Content1 = "Hello world1\nHello world2";
    public readonly string Content2 = "HELLO WORLD1\nHELLO WORLD2";

    public readonly string sourceFolderPath1;
    public readonly string sourceFolderPath2;
    public readonly string TargetFolder;
    public readonly string SourceFile1;
    public readonly string SourceFile2;

    public readonly string SourceFolder1;
    public readonly string SourceFolder2;

    public Profile Backup1_KeepStructure { get; private set; }
    public Profile Backup2_NoKeepStructure { get; private set; }

    // ArchSTests
    //      - SourceFolderTest1
    //          - Folder1 
    //              - FileName.txt
    //          - FileName.txt
    //      - SourceFolderTest2
    //          - Folder1 
    //          - Folder2 
    //              - FileName.txt
    //.     - TargetFolder
    public BackUpControllerFixture()
    {
        Controller = new BackupService();

        var projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..")); // this will be ../bin/Debug/net8.0 
        sourceFolderPath1 = Path.Combine(projectRoot, "SourceFolderTest1");
        sourceFolderPath2 = Path.Combine(projectRoot, "SourceFolderTest2");

        // Paths for SourceFolderTest1
        SourceFolder1 = Path.Combine(sourceFolderPath1, "Folder1");
        SourceFile1 = Path.Combine(sourceFolderPath1, "FileName.txt");
        var sourceFolderFile = Path.Combine(SourceFolder1, "FileName.txt");

        // Target
        TargetFolder = Path.Combine(projectRoot, "TargetFolderTest");

        // Paths for SourceFolderTest2
        SourceFolder2 = Path.Combine(sourceFolderPath2, "Folder1");
        var sourceFile2_ = Path.Combine(SourceFolder2, "File.txt");
        var sourceFolder2_ = Path.Combine(sourceFolderPath2, "Folder2");
        SourceFile2 = Path.Combine(sourceFolder2_, "FileName.txt");

        Directory.CreateDirectory(SourceFolder1);
        Directory.CreateDirectory(TargetFolder);
        Directory.CreateDirectory(SourceFolder2);
        Directory.CreateDirectory(sourceFolder2_);

        File.WriteAllText(SourceFile1, Content1);
        File.WriteAllText(sourceFolderFile, Content2);
        File.WriteAllText(SourceFile2, Content2);
        File.WriteAllText(sourceFile2_, Content2);

        // Test: well creation of the backup, update the keepUpdate flag, test keep structure expected outcome 
        Backup1_KeepStructure = new Profile("Backup1",
            new List<string> { SourceFolder1, sourceFolder2_ },
            new List<string> { SourceFile1 },
            TargetFolder, true, false);
        Controller.AddProfile(Backup1_KeepStructure);

        var mapping = BackupFileManager.GetProfileMapping(
            new List<string> { SourceFolder1, SourceFolder2 },
            new List<string> { SourceFile1, SourceFile2 }
        );

        // Test: well mapping for files and folders named the same 
        Backup2_NoKeepStructure = new Profile("Backup2",
            new List<string> { SourceFolder1, SourceFolder2 },
            new List<string> { SourceFile1, SourceFile2 },
            TargetFolder, false, true, mapping);
        Controller.AddProfile(Backup2_NoKeepStructure);

    }

    public void Dispose()
    {
        Action<string> DeleteDir = (path) =>
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        };
        DeleteDir(TargetFolder);
        DeleteDir(sourceFolderPath1);
        DeleteDir(sourceFolderPath2);
    }

}


[Collection("SequentialTests")]
[TestCaseOrderer("Tests.TestOrderer", "ArchSTests")]
public class UniTest : IClassFixture<BackUpControllerFixture>
{
    private readonly ITestOutputHelper _output; // _output.WriteLine(...) helps to debug 
    private readonly BackUpControllerFixture _fixture;

    public UniTest(BackUpControllerFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    Func<string, string> ReadTextShared = (string path) =>
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var sr = new StreamReader(fs);
        return sr.ReadToEnd();
    };

    [Fact]
    public async Task TestBackup()
    {
        await Task.Delay(1000); 
        // common parent must be up to projectRoot 
        var filePath1 = Path.Combine(_fixture.TargetFolder, "Backup1", "SourceFolderTest1", "Folder1", "FileName.txt");
        var filePath2 = Path.Combine(_fixture.TargetFolder, "Backup1", "SourceFolderTest2", "Folder2", "FileName.txt");
        var filePath3 = Path.Combine(_fixture.TargetFolder, "Backup1", "SourceFolderTest1", "FileName.txt");

        Assert.True(File.Exists(filePath1));
        Assert.True(File.Exists(filePath2));
        Assert.True(File.Exists(filePath3));

        Assert.Equal(_fixture.Content2, ReadTextShared(filePath1));
        Assert.Equal(_fixture.Content2, ReadTextShared(filePath2));
        Assert.Equal(_fixture.Content1, ReadTextShared(filePath3));
    }

    [Fact]
    public void UpdateKeepTrackFlag()
    {
        Profile? updatedProfile = _fixture.Controller.GetProfile(_fixture.Backup1_KeepStructure.Id);
        Assert.NotNull(updatedProfile);
        Assert.False(((Profile)updatedProfile).KeepTrackFlag);
        _fixture.Controller.ChangeTrackFlag(_fixture.Backup1_KeepStructure.Id, true);
        updatedProfile = _fixture.Controller.GetProfile(_fixture.Backup1_KeepStructure.Id);
        Assert.NotNull(updatedProfile);
        Assert.True(((Profile)updatedProfile).KeepTrackFlag);
    }

    [Fact]
    public async Task UpdateBackup()
    {
        File.WriteAllText(_fixture.SourceFile1, "Modified");
        Profile? updatedProfile = _fixture.Controller.GetProfile(_fixture.Backup1_KeepStructure.Id);
        Assert.NotNull(updatedProfile);
        Profile profile = (Profile)updatedProfile;
        var (archive1, _) = ProfileHandler.GetArchiveItemToUpdate(profile);
        Assert.True(archive1.Items.Count == 1);
        var targetFilePath = Path.Combine(_fixture.TargetFolder, "Backup1", "SourceFolderTest1", "FileName.txt");
        Assert.Equal(targetFilePath, archive1.Items[0].TargetPath);
        Assert.Equal(_fixture.SourceFile1, archive1.Items[0].SourcePath);
        await _fixture.Controller.UpdateProfileAsync(profile.Id);
        var (archive2, _) = ProfileHandler.GetArchiveItemToUpdate(profile);
        Assert.True(archive2.Items.Count == 0);
        var filePath = Path.Combine(_fixture.TargetFolder, "Backup1", "SourceFolderTest1", "FileName.txt");
        Assert.Equal("Modified", ReadTextShared(filePath));
    }

    [Fact]
    public void TestMappingOfFiles()
    {
        var pathFolderName1 = Path.Combine(_fixture.TargetFolder,"Backup2", "Folder1(1)");
        var pathFolderName2 = Path.Combine(_fixture.TargetFolder,"Backup2", "Folder1(2)");
        Assert.True(Directory.Exists(pathFolderName1));
        Assert.True(Directory.Exists(pathFolderName2));
        var pathFileName1 = Path.Combine(_fixture.TargetFolder, "Backup2", "FileName(1).txt");
        var pathFileName2 = Path.Combine(_fixture.TargetFolder, "Backup2", "FileName(2).txt");
        Assert.True(File.Exists(pathFileName1));
        Assert.True(File.Exists(pathFileName2));
    }

    [Fact]
    public void TestReflectionProfileMetadataUpdate()
    {
        Profile? profile1 = _fixture.Controller.GetProfile(_fixture.Backup1_KeepStructure.Id);
        Profile profile = (Profile)profile1!;
        DateTime time = profile.SavedAt;
        BackupFileManager.UpdateProfileProperty<DateTime>(profile, "SavedAt", DateTime.UtcNow);
        Profile? profile2 = _fixture.Controller.GetProfile(_fixture.Backup1_KeepStructure.Id);
        Assert.True(profile2!.SavedAt > time);
    }

    [Fact]
    public void TestDeleteBackups()
    {
        _fixture.Controller.DeleteProfileById(_fixture.Backup1_KeepStructure.Id, true);
        Profile? profile = _fixture.Controller.GetProfile(_fixture.Backup1_KeepStructure.Id);
        Assert.Null(profile);
        _fixture.Controller.DeleteProfileById(_fixture.Backup2_NoKeepStructure.Id, true);
        profile = _fixture.Controller.GetProfile(_fixture.Backup2_NoKeepStructure.Id);
        Assert.Null(profile);
    }
}