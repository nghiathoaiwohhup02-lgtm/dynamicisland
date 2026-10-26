namespace DynamicIsland.Core;

/// <summary>Module nhận file người dùng kéo thả vào đảo (khay file).</summary>
public interface IFileDropTarget
{
    void AcceptFiles(IEnumerable<string> paths);
}
