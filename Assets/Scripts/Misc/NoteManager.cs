using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NoteManager : MonoBehaviour
{
    public List<GameObject> notes = new();
    public Dictionary<GameObject, int> noteOrder = new();
    public Dictionary<int, int> noteCount = new();
    // Start is called before the first frame update
    void Start()
    {
        
    }
    public void Refresh()
    {
        var count = transform.childCount;
        ResetCounter();
        for (int i = 0; i < count; i++)
        {
            var child = transform.GetChild(i);
            var tap = child.GetComponent<TapDrop>();
            var hold = child.GetComponent<HoldDrop>();
            var star = child.GetComponent<StarDrop>();

            if (tap != null)
                noteOrder.Add(tap.gameObject, noteCount[tap.startPosition]++);
            else if (hold != null)
                noteOrder.Add(hold.gameObject, noteCount[hold.startPosition]++);
            else if (star != null && !star.isNoHead)
                noteOrder.Add(star.gameObject, noteCount[star.startPosition]++);
            notes.Add(child.gameObject);
        }
        ResetCounter();
    }
    void ResetCounter()
    {
        noteCount.Clear();
        for (int i = 1; i < 9; i++)
            noteCount.Add(i, 0);
    }
    public int GetOrder(GameObject obj) => noteOrder[obj];
    public bool CanJudge(GameObject obj, int pos)
    {
        if (!noteOrder.ContainsKey(obj))
            return false;
        var index = noteOrder[obj];
        var nowIndex = noteCount[pos];

        return index <= nowIndex;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
