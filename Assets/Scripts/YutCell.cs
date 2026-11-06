using UnityEngine;

[DisallowMultipleComponent]
public sealed class YutCell : MonoBehaviour
{
    [SerializeField] private int cellId;

    public int CellId => cellId;
}
