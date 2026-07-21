public class ToolManifestTests
{
    static string manifest =
        """
        {
          "version": 1,
          "isRoot": true,
          "tools": {
            "dotnet-ef": {
              "version": "5.0.0",
              "commands": [
                "dotnet-ef"
              ],
              "rollForward": false
            },
            "packageupdate": {
              "version": "4.0.0",
              "commands": [
                "packageupdate"
              ],
              "pinned": true
            }
          }
        }
        """;

    [Test]
    public async Task ReadsTools()
    {
        var entries = ToolManifest.Read(Encoding.UTF8.GetBytes(manifest));

        await Assert.That(entries).Count().IsEqualTo(2);

        await Assert.That(entries[0].Package).IsEqualTo("dotnet-ef");
        await Assert.That(entries[0].Version).IsEqualTo("5.0.0");
        await Assert.That(entries[0].Pinned).IsFalse();

        await Assert.That(entries[1].Package).IsEqualTo("packageupdate");
        await Assert.That(entries[1].Version).IsEqualTo("4.0.0");
        await Assert.That(entries[1].Pinned).IsTrue();
    }

    [Test]
    public async Task VersionOffsetsPointAtTheVersionValue()
    {
        var bytes = Encoding.UTF8.GetBytes(manifest);

        foreach (var entry in ToolManifest.Read(bytes))
        {
            var value = Encoding.UTF8.GetString(bytes, entry.VersionStart, entry.VersionLength);
            await Assert.That(value).IsEqualTo(entry.Version);
        }
    }

    [Test]
    public async Task ApplyUpdatesOnlyReplacesVersions()
    {
        var bytes = Encoding.UTF8.GetBytes(manifest);
        var entries = ToolManifest.Read(bytes);

        // The first replacement is longer than the value it replaces, so the offsets of the
        // second tool would be stale if the splices were not applied from the end of the file
        var updated = ToolManifest.ApplyUpdates(
            bytes,
            [
                (entries[0], NuGetVersion.Parse("9.0.1-preview.1")),
                (entries[1], NuGetVersion.Parse("4.3.0"))
            ]);

        var result = Encoding.UTF8.GetString(updated);

        await Assert.That(result)
            .IsEqualTo(
                manifest
                    .Replace("\"5.0.0\"", "\"9.0.1-preview.1\"")
                    .Replace("\"4.0.0\"", "\"4.3.0\""));
    }

    [Test]
    public async Task HandlesByteOrderMark()
    {
        byte[] bytes = [..Encoding.UTF8.GetPreamble(), ..Encoding.UTF8.GetBytes(manifest)];

        var entries = ToolManifest.Read(bytes);

        await Assert.That(entries).Count().IsEqualTo(2);

        var value = Encoding.UTF8.GetString(bytes, entries[0].VersionStart, entries[0].VersionLength);
        await Assert.That(value).IsEqualTo("5.0.0");

        var updated = ToolManifest.ApplyUpdates(bytes, [(entries[0], NuGetVersion.Parse("9.0.1"))]);

        // The byte order mark must survive
        await Assert.That(updated.AsSpan().StartsWith(Encoding.UTF8.GetPreamble())).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(updated))
            .IsEqualTo("﻿" + manifest.Replace("\"5.0.0\"", "\"9.0.1\""));
    }

    [Test]
    public async Task NoToolsSection()
    {
        var content =
            """
            {
              "version": 1,
              "isRoot": true
            }
            """;

        var entries = ToolManifest.Read(Encoding.UTF8.GetBytes(content));

        await Assert.That(entries).IsEmpty();
    }

    [Test]
    public async Task ToolWithoutVersionIsIgnored()
    {
        var content =
            """
            {
              "version": 1,
              "tools": {
                "dotnet-ef": {
                  "commands": [
                    "dotnet-ef"
                  ]
                },
                "packageupdate": {
                  "version": "4.0.0"
                }
              }
            }
            """;

        var entries = ToolManifest.Read(Encoding.UTF8.GetBytes(content));

        await Assert.That(entries).Count().IsEqualTo(1);
        await Assert.That(entries[0].Package).IsEqualTo("packageupdate");
    }

    [Test]
    public async Task CommandsAreNotTreatedAsVersions()
    {
        var content =
            """
            {
              "tools": {
                "dotnet-ef": {
                  "commands": [
                    "version"
                  ],
                  "version": "5.0.0"
                }
              }
            }
            """;

        var bytes = Encoding.UTF8.GetBytes(content);
        var entries = ToolManifest.Read(bytes);

        await Assert.That(entries).Count().IsEqualTo(1);
        await Assert.That(entries[0].Version).IsEqualTo("5.0.0");

        var value = Encoding.UTF8.GetString(bytes, entries[0].VersionStart, entries[0].VersionLength);
        await Assert.That(value).IsEqualTo("5.0.0");
    }

    [Test]
    public async Task MalformedManifestThrows()
    {
        var read = () => ToolManifest.Read("not json"u8.ToArray());

        await Assert.That(read).Throws<JsonException>();
    }
}
