using EmberDeck.Run;
using UnityEditor;
using UnityEngine;

namespace EmberDeck.EditorTools
{
    public static class SaveTools
    {
        [MenuItem("EmberDeck/Delete Saved Run")]
        public static void DeleteSave()
        {
            bool existed = RunSave.Exists();
            RunSave.Delete();
            Debug.Log(existed
                ? "[EmberDeck] Saved run deleted."
                : "[EmberDeck] There was no saved run.");
        }
    }
}
