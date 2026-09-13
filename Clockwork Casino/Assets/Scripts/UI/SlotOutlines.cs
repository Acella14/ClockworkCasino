using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace ClockworkCasino.UI
{
    public sealed class SlotOutlines : MonoBehaviour
    {
        [FormerlySerializedAs("_container")]
        [SerializeField]
        private RectTransform _slotContainer;

        [FormerlySerializedAs("_autoGrabChildren")]
        [SerializeField]
        private bool _automaticallyCacheChildren = true;

        [FormerlySerializedAs("_slots")]
        [SerializeField]
        private List<GameObject> _slotObjects = new();

        private void Reset()
        {
            _slotContainer = transform as RectTransform;
            CacheChildSlots();
        }

        public void SetVisibleCount(int visibleCount)
        {
            EnsureSlotCache();

            visibleCount = Mathf.Clamp(
                visibleCount,
                0,
                _slotObjects.Count);

            for (int index = 0;
                 index < _slotObjects.Count;
                 index++)
            {
                GameObject slotObject = _slotObjects[index];

                if (slotObject != null)
                    slotObject.SetActive(index < visibleCount);
            }

            RebuildLayout();
        }

        public void HideAll()
        {
            SetVisibleCount(0);
        }

        public List<Vector2> GetLocalSlotCenters(
            int requestedCount,
            RectTransform targetSpace,
            Camera canvasCamera)
        {
            var positions = new List<Vector2>();

            if (targetSpace == null)
                return positions;

            EnsureSlotCache();

            int availableCount = Mathf.Min(
                requestedCount,
                _slotObjects.Count);

            SetVisibleCount(availableCount);

            for (int index = 0;
                 index < availableCount;
                 index++)
            {
                GameObject slotObject = _slotObjects[index];

                if (slotObject == null)
                    continue;

                RectTransform slotRectTransform =
                    slotObject.transform as RectTransform;

                if (slotRectTransform == null)
                    continue;

                Vector3[] corners = new Vector3[4];
                slotRectTransform.GetWorldCorners(corners);

                Vector3 worldCenter =
                    (corners[0] + corners[2]) * 0.5f;

                Vector2 screenPosition =
                    RectTransformUtility.WorldToScreenPoint(
                        canvasCamera,
                        worldCenter);

                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    targetSpace,
                    screenPosition,
                    canvasCamera,
                    out Vector2 localPosition);

                positions.Add(localPosition);
            }

            return positions;
        }

        private void EnsureSlotCache()
        {
            if (_slotContainer == null)
                _slotContainer = transform as RectTransform;

            if (_automaticallyCacheChildren
                || _slotObjects.Count == 0)
            {
                CacheChildSlots();
            }
        }

        private void CacheChildSlots()
        {
            if (_slotContainer == null)
                _slotContainer = transform as RectTransform;

            _slotObjects.Clear();

            if (_slotContainer == null)
                return;

            for (int index = 0;
                 index < _slotContainer.childCount;
                 index++)
            {
                _slotObjects.Add(
                    _slotContainer.GetChild(index).gameObject);
            }
        }

        private void RebuildLayout()
        {
            if (_slotContainer == null)
                return;

            LayoutRebuilder.ForceRebuildLayoutImmediate(
                _slotContainer);

            Canvas.ForceUpdateCanvases();
        }
    }
}