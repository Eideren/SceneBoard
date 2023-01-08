namespace SceneBoard
{
    using System.Collections.Generic;
    using UnityEngine;

    [CreateAssetMenu(menuName = "Board Asset")]
    public class BoardAsset : ScriptableObject, IBoardStorage
    {
        [SerializeReference] public List<IStorable> Objs = new ();
        public List<string> AddedByDefault = new ();
        
        List<IStorable> IBoardStorage.Objs => Objs;
        List<string> IBoardStorage.AddedByDefault => AddedByDefault;
    }
}