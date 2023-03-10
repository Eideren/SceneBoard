namespace SceneBoard
{
    using System.Collections.Generic;
    using UnityEngine;

    public class SceneBoardStorage : MonoBehaviour, IBoardStorage
    {
        [SerializeReference]public List<IStorable> objs = new ();
        public List<string> AddedByDefault = new ();
        
        List<IStorable> IBoardStorage.Objs => objs;
        List<string> IBoardStorage.AddedByDefault => AddedByDefault;
    }
}