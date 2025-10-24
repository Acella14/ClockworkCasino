using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ClockworkCasino.UI
{
    public class SlotOutlines : MonoBehaviour
    {
        [SerializeField] private RectTransform _container;
        [SerializeField] private bool _autoGrabChildren = true;
        [SerializeField] private List<GameObject> _slots = new();

        void Reset()
        {
            _container = (RectTransform)transform;
            AutoGrab();
        }

        void AutoGrab()
        {
            if (!_container) _container = (RectTransform)transform;
            _slots.Clear();
            for (int i = 0; i < _container.childCount; i++)
                _slots.Add(_container.GetChild(i).gameObject);
        }

        public void SetCount(int count)
        {
            if (_autoGrabChildren || _slots.Count == 0) AutoGrab();

            for (int i = 0; i < _slots.Count; i++)
                _slots[i].SetActive(i < count);

            LayoutRebuilder.ForceRebuildLayoutImmediate(_container);
            Canvas.ForceUpdateCanvases();
        }

        public void HideAll() => SetCount(0);
    }
}
