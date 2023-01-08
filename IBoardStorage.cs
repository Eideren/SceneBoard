namespace SceneBoard
{
    using System.Collections.Generic;

    public interface IBoardStorage
    {
        List<IStorable> Objs { get; }
        List<string> AddedByDefault { get; }
    }
}