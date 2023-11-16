using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Public.UGUITools.UIMenu2
{
    public abstract class MenuItemBase : MonoBehaviour
    {
        [SerializeField,Tooltip("元件最小可能的高度或寬度")] protected float minSize = 30f;
        protected RectTransform rectTransform = null;
        protected RectTransform RectTransform => rectTransform ? rectTransform : (rectTransform = GetComponent(typeof(RectTransform)) as RectTransform);

        public void SetItemPosition(Vector2 targetPos)
        {
            RectTransform.anchoredPosition = targetPos;
        }

        public void SetDisplay(bool bDisplay)
        {
            gameObject.SetActive(bDisplay);
        }

        public virtual float GetMinSize(ScrollingMenu.EDirection direction)
        {
            return minSize;
        }

        public virtual float GetItemSize(object objData)
        {
            return minSize;
        }
        public virtual void LockInteractable(bool bLock)
        {
            var allSelectable = GetComponentsInChildren<Selectable>(true);
            foreach (var selectable in allSelectable)
            {
                selectable.interactable = !bLock;
            }
        }

        public abstract void UpdateItemView(object objData);

        public virtual void ShowIn(Vector2 targetPos, int iShowOrder, UnityAction endCallback)
        {
            RectTransform.anchoredPosition = targetPos;
            endCallback?.Invoke();
        }

        public virtual void ShowOut(Vector2 targetPos, int showOrder, UnityAction endCallback)
        {
            RectTransform.anchoredPosition = targetPos;
            endCallback?.Invoke();
        }

        public virtual void ShowMoveUp(Vector2 targetPos, int moveUnitDistance, int showOrder, UnityAction endCallback)
        {
            RectTransform.anchoredPosition = targetPos;
            endCallback?.Invoke();
        }

        public virtual void ShowMoveDown(Vector2 targetPos, int moveUnitDistance, int showOrder, UnityAction endCallback)
        {
            RectTransform.anchoredPosition = targetPos;
            endCallback?.Invoke();
        }

        public virtual void SetItemPosition(Vector2 targetPos, int showOrder, UnityAction endCallback)
        {
            RectTransform.anchoredPosition = targetPos;
            endCallback?.Invoke();
        }

    }
}