namespace SceneBoard.Editor
{
    using System.Collections.Generic;
    using UnityEngine;

    public class SceneBoardStorage : MonoBehaviour
    {
        [SerializeReference]public List<IStorable> objs = new ();
        public List<string> AddedByDefault = new ();

        public interface IStorable
        {
        }
    }
}