using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Public.UGUITools.UIMenu2
{
    public class ScrollingMenu : MonoBehaviour
    {
        public LinkedList<MenuItemBase> MenuItems { get; } = new LinkedList<MenuItemBase>();

    #region 基準變數

        private const int OutViewItem = 2;

        public enum EDirection
        {
            VerticalUp2Down,
            HorizontalLeft2Right
        }

        [SerializeField] private bool initInAwake = true;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private MenuItemBase prefabMenuItemBase;
        [SerializeField] private EDirection direction = EDirection.VerticalUp2Down;

        private float scrollRectSize;
        private int itemNum;
        private RectTransform menuContentRect;
        private LinkedListNode<MenuItemBase> topMenuItem; //計算時以這個為基準
        private MenuItemBase mAniHighestItemBase;
        private MenuItemBase lowestItemBase;
        private List<object> itemDatas;
        private List<Vector2> itemPositions;
        private float contentSizeInView;
        private float contentSizeByData;
        private int topDataIndex;
        private int bottomDataIndex;
        [SerializeField]private float topBoundaryLine;
        [SerializeField]private float bottomBoundaryLine;
        private UnityAction endCallback;

    #endregion

    #region 暫存用變數

        private LinkedListNode<MenuItemBase> bottomMenuItem; //由m_topMenuItem動態取得(降低記憶體配置率用)
        private float currentContentTop;
        private float currentContentBottom;

    #endregion

    #region 初始化顯示元件

        private void Reset()
        {
            scrollRect = GetComponent<ScrollRect>();
        }

        private void Awake()
        {
            if (initInAwake)
            {
                InitScrollingMenu();
            }
        }

        public void InitScrollingMenu()
        {
            if (scrollRect == null)
            {
                Debug.LogError("m_scrollRect NULL!!");
                return;
            }

            if (prefabMenuItemBase == null)
            {
                Debug.LogError("m_scrollRect NULL!!");
                return;
            }

            InitScrollRect();
            InitContentRect();
            var item = prefabMenuItemBase.GetComponent<MenuItemBase>();
            var fMenuMinSize = item.GetMinSize(direction);
            itemNum = Mathf.CeilToInt(scrollRectSize / fMenuMinSize) + OutViewItem;
            CreateMenuItems();
            scrollRect.enabled = true;
        }

        private void InitScrollRect()
        {
            scrollRect.horizontal = direction == EDirection.HorizontalLeft2Right;
            scrollRect.vertical = direction == EDirection.VerticalUp2Down;
            scrollRect.onValueChanged.AddListener(OnScrollRectValueChanged); //監聽方式
        }

        private void InitContentRect()
        {
            menuContentRect = scrollRect.content;
            var scrollRectTransform = scrollRect.GetComponent<RectTransform>();
            if (scrollRectTransform != null)
            {
                var rect = scrollRectTransform.rect;
                scrollRectSize = rect.height * (1 - (int) direction) + rect.width * (int) direction;
            }
            SetContentRectLayout(); 
        }            
        private void SetContentRectLayout()
        {
            if (direction == EDirection.VerticalUp2Down)
            {
                menuContentRect.sizeDelta = new Vector2(0, 0);
                menuContentRect.anchorMin = new Vector2(0, 1);
                menuContentRect.anchorMax = new Vector2(1, 1);
                menuContentRect.pivot = new Vector2(0.5f, 1);
            }
            else
            {
                menuContentRect.sizeDelta = new Vector2(0, 0);
                menuContentRect.anchorMin = new Vector2(0, 0);
                menuContentRect.anchorMax = new Vector2(0, 1);
                menuContentRect.pivot = new Vector2(0, 0.5f);
            }
        }

        private void CreateMenuItems()
        {
            var iNowItemNum = MenuItems.Count;
            if (iNowItemNum == itemNum)
            {
                return;
            }

            for (var i = 0; i < iNowItemNum; ++i)
            {
                Destroy(MenuItems.First.Value);
                MenuItems.RemoveFirst();
            }

            //創造最低圖層元件
            if (lowestItemBase == null)
            {
                lowestItemBase = NewMenuItem(menuContentRect);
                lowestItemBase.SetDisplay(false);
            }

            //生成實際設定的Item並重複使用它們
            for (var i = 0; i < itemNum; ++i)
            {
                var newItem = NewMenuItem(menuContentRect);
                newItem.SetDisplay(false);
                MenuItems.AddLast(newItem);
            }

            topMenuItem = MenuItems.First;
            ResetTopAndBottomLines();
            //創造最高圖層元件
            if (mAniHighestItemBase == null)
            {
                mAniHighestItemBase = NewMenuItem(menuContentRect);
                mAniHighestItemBase.SetDisplay(false);
            }
        }

        private MenuItemBase NewMenuItem(Transform itemRoot)
        {
            var goNewItem = Instantiate(prefabMenuItemBase.gameObject, itemRoot, false);
            return goNewItem.GetComponent(typeof(MenuItemBase)) as MenuItemBase;
        }

    #endregion

    #region 獲取元件資訊

        public int ItemDatasCount => itemDatas.Count;

    #endregion

    #region 設定元件資料

        public float ViewNormalizedPosition
        {
            get
            {
                float normalizedPosition = 0;
                if (direction == EDirection.VerticalUp2Down)
                {
                    normalizedPosition = 1 - scrollRect.verticalNormalizedPosition;
                }
                else if (direction == EDirection.HorizontalLeft2Right)
                {
                    normalizedPosition = scrollRect.horizontalNormalizedPosition;
                }

                return normalizedPosition;
            }
            set
            {
                scrollRect.verticalNormalizedPosition = 1 - value;
                scrollRect.horizontalNormalizedPosition = value;
            }
        }

        public void SetInitMenuData(List<object> initItemDatas, UnityAction uaCallback, bool resetPosition = true, float normalizedPosition = 0)
        {
            //Debug.Log("setInitMenuData");
            endCallback = uaCallback;
            //scrollRect.enabled = false;
            SetAllItemLock(true);
            itemDatas = initItemDatas ?? new List<object>();

            ResetTopAndBottomLines();
            CountAllItemsPosition();
            ReSizeContent(contentSizeByData, true);
            InitMenuDataToView();
            //scrollRect.enabled = true;
            if (resetPosition)
            {
                scrollRect.verticalNormalizedPosition = 1 - normalizedPosition;
                scrollRect.horizontalNormalizedPosition = normalizedPosition;
            }
            else
            {
                OnScrollRectValueChanged(Vector2.zero);
            }
        }

        private void InitMenuDataToView()
        {
            topDataIndex = 0;
            var trivialNode = MenuItems.First;
            var iMoveCount = MenuItems.Count;
            for (var i = 0; i < MenuItems.Count; ++i)
            {
                var moveCount = iMoveCount;
                if (i < itemDatas.Count)
                {
                    var v2ShowPos = GetItemPosition(i);
                    if (trivialNode != null)
                    {
                        trivialNode.Value.SetDisplay(true);
                        trivialNode.Value.UpdateItemView(itemDatas[i]);
                        trivialNode.Value.ShowIn(v2ShowPos, i, delegate
                        {
                            --moveCount;
                            if (moveCount == 0)
                            {
                                DoCallback();
                            }
                        });
                    }

                    bottomDataIndex = i;
                }
                else
                {
                    trivialNode?.Value.SetDisplay(false);
                    --moveCount;
                    if (moveCount == 0)
                    {
                        DoCallback();
                    }
                }

                trivialNode = trivialNode?.Next;
            }
            topMenuItem = MenuItems.First;
        }

        public void AddItem(object objItemData, UnityAction uaCallback)
        {
            endCallback = uaCallback;
            SetAllItemLock(true);
            var iAddDataIndex = itemDatas.Count;
            itemDatas.Add(objItemData);
            itemPositions.Add(GetNewItemPosition());
            var fAddItemSize = lowestItemBase.GetItemSize(objItemData);
            //Debug.Log("fAddItemSize = " + fAddItemSize);
            contentSizeByData += fAddItemSize;
            ReSizeContent(fAddItemSize, false);
            scrollRect.verticalNormalizedPosition = 0;
            var trivialNode = MenuItems.First;
            if (iAddDataIndex < itemNum)
            {
                for (var i = 0; i < iAddDataIndex; ++i)
                {
                    trivialNode = trivialNode?.Next;
                }

                if (trivialNode != null)
                {
                    trivialNode.Value.SetDisplay(true);
                    trivialNode.Value.UpdateItemView(objItemData);
                    trivialNode.Value.ShowIn(GetItemPosition(iAddDataIndex), 0, DoCallback);
                }

                bottomDataIndex = iAddDataIndex;
                bottomBoundaryLine -= fAddItemSize;
            }
            else
            {
                DoCallback();
            }
        }

        public void AddItems(List<object> objItemData, UnityAction uaCallback)
        {
            endCallback = uaCallback;
            SetAllItemLock(true);
            int targetMoveCount = objItemData.Count;
            int currentMove = 0;
            float totalAddItemSize = 0;
            foreach (var objItem in objItemData)
            {
                var addDataIndex = itemDatas.Count;
                itemDatas.Add(objItem);
                itemPositions.Add(GetNewItemPosition());
                var addPosition = GetItemPosition(addDataIndex);
                var addItemSize = lowestItemBase.GetItemSize(objItem);
                totalAddItemSize += addItemSize;
                contentSizeByData += addItemSize;
                var moveCount = targetMoveCount;
                if (addDataIndex < itemNum)
                {
                    var trivialNode = MenuItems.First;
                    for (var i = 0; i < addDataIndex; ++i)
                    {
                        trivialNode = trivialNode?.Next;
                    }

                    if (trivialNode != null)
                    {
                        trivialNode.Value.SetDisplay(true);
                        trivialNode.Value.UpdateItemView(objItem);
                        trivialNode.Value.ShowIn(addPosition, currentMove, delegate
                        {
                            --moveCount;
                            if (moveCount == 0)
                            {
                                DoCallback();
                            }
                        });
                    }

                    ++currentMove;
                    bottomDataIndex = addDataIndex;
                }
                else
                {
                    --moveCount;
                    if (moveCount == 0)
                    {
                        DoCallback();
                    }
                }
            }

            ReSizeContent(totalAddItemSize, false);
        }

        public void RemoveItem(int itemIndex, UnityAction uaCallback)
        {
            endCallback = uaCallback;
            SetAllItemLock(true);
            if (itemIndex < itemDatas.Count && itemIndex >= 0)
            {
                var removeData = itemDatas[itemIndex];
                itemDatas.RemoveAt(itemIndex);
                var bItemInview = false;
                var bMoveViewUp = false;
                var bDisableBottomItem = false;
                if (itemIndex >= topDataIndex && itemIndex <= bottomDataIndex)
                {
                    bItemInview = true;
                    if (bottomDataIndex == itemDatas.Count)
                    {
                        //拉到最底的狀況
                        //Debug.Log("拉到最底的狀況");
                        if (topDataIndex > 0)
                        {
                            --topDataIndex;
                            bMoveViewUp = true;
                        }
                        else
                        {
                            //畫面未滿的狀況
                            bDisableBottomItem = true;
                        }

                        --bottomDataIndex;
                    }
                }
                else if (itemIndex < topDataIndex)
                {
                    //在畫面上方
                    bMoveViewUp = true;
                    --topDataIndex;
                    --bottomDataIndex;
                }

                if (bItemInview)
                {
                    var trivalNode = topMenuItem;
                    for (var i = topDataIndex; i <= bottomDataIndex; ++i)
                    {
                        if (bMoveViewUp)
                        {
                            if (i == itemIndex - 1)
                            {
                                trivalNode.Value.SetDisplay(false);
                                break;
                            }
                        }
                        else
                        {
                            if (i == itemIndex)
                            {
                                trivalNode.Value.SetDisplay(false);
                                break;
                            }
                        }

                        trivalNode = GetCircularlyLinkListNextNode(trivalNode);
                    }

                    if (bDisableBottomItem)
                    {
                        trivalNode.Value.SetDisplay(false);
                    }

                    var fRemoveItemSize = lowestItemBase.GetItemSize(removeData);
                    itemPositions.RemoveAt(itemIndex);
                    CountAllItemsPosition();
                    lowestItemBase.SetDisplay(true);
                    lowestItemBase.UpdateItemView(removeData);
                    lowestItemBase.ShowOut(GetItemPosition(itemIndex), 0, delegate
                    {
                        trivalNode.Value.SetDisplay(true);
                        if (bMoveViewUp)
                        {
                            menuContentRect.anchoredPosition -= new Vector2((int) direction, 1 - (int) direction) * fRemoveItemSize;
                            bottomMenuItem = GetCircularlyLinkListPreviousNode(topMenuItem);
                            bottomMenuItem.Value.SetDisplay(true);
                            bottomMenuItem.Value.UpdateItemView(itemDatas[topDataIndex]);
                            bottomMenuItem.Value.SetItemPosition(GetItemPosition(topDataIndex));
                            topMenuItem = bottomMenuItem;
                            MoveTopAndBottomLines(fRemoveItemSize, fRemoveItemSize);
                        }

                        lowestItemBase.SetDisplay(false);
                        var iMoveCount = bottomDataIndex - topDataIndex + 1;
                        trivalNode = topMenuItem;
                        for (var i = topDataIndex; i <= bottomDataIndex; ++i)
                        {
                            var moveCount = iMoveCount;
                            if (i >= itemIndex)
                            {
                                trivalNode.Value.SetDisplay(true);
                                trivalNode.Value.UpdateItemView(itemDatas[i]);
                                if (!bMoveViewUp)
                                {
                                    trivalNode.Value.ShowMoveUp(GetItemPosition(i), 1, 1, delegate
                                    {
                                        --moveCount;
                                        if (moveCount == 0)
                                        {
                                            ReSizeContent(-fRemoveItemSize, false);
                                            DoCallback();
                                        }
                                    });
                                }
                                else
                                {
                                    trivalNode.Value.SetItemPosition(GetItemPosition(i), 1, delegate
                                    {
                                        --moveCount;
                                        if (moveCount == 0)
                                        {
                                            ReSizeContent(-fRemoveItemSize, false);
                                            DoCallback();
                                        }
                                    });
                                }
                            }
                            else
                            {
                                if (bMoveViewUp)
                                {
                                    trivalNode.Value.ShowMoveDown(GetItemPosition(i), 1, 1, delegate
                                    {
                                        --moveCount;
                                        if (moveCount == 0)
                                        {
                                            ReSizeContent(-fRemoveItemSize, false);
                                            DoCallback();
                                        }
                                    });
                                }
                                else
                                {
                                    --moveCount;
                                    if (moveCount == 0)
                                    {
                                        ReSizeContent(-fRemoveItemSize, false);
                                        DoCallback();
                                    }
                                }
                            }

                            trivalNode = GetCircularlyLinkListNextNode(trivalNode);
                        }

                        if (bDisableBottomItem)
                        {
                            trivalNode.Value.SetDisplay(false);
                        }
                    });
                }
            }
            else
            {
                Debug.LogError("物件" + itemIndex + "不存在!!");
                DoCallback();
            }
        }

        public void InsertItem(int iInsertIndex, object objItemData, UnityAction uaCallback)
        {
            endCallback = uaCallback;
            SetAllItemLock(true);
            if (iInsertIndex < itemDatas.Count && iInsertIndex >= 0)
            {
                itemDatas.Insert(iInsertIndex, objItemData);
                CountAllItemsPosition();
                var fAddItmSize = lowestItemBase.GetItemSize(objItemData);
                if (topDataIndex == 0 && bottomDataIndex < itemNum - 1)
                {
                    ++bottomDataIndex;
                }

                var bMoveDown = iInsertIndex < topDataIndex;
                if (bMoveDown)
                {
                    ++bottomDataIndex;
                    ++topDataIndex;

                    menuContentRect.anchoredPosition += new Vector2((int) direction, 1 - (int) direction) * fAddItmSize;
                    topMenuItem.Value.SetDisplay(true);
                    topMenuItem.Value.UpdateItemView(itemDatas[bottomDataIndex]);
                    topMenuItem.Value.SetItemPosition(GetItemPosition(bottomDataIndex));
                    topMenuItem = GetCircularlyLinkListNextNode(topMenuItem);
                    MoveTopAndBottomLines(-fAddItmSize, -fAddItmSize);
                }

                var trivalNode = topMenuItem;
                var iMoveCount = bottomDataIndex - topDataIndex + 1;
                for (var i = topDataIndex; i <= bottomDataIndex; ++i)
                {
                    if (i > iInsertIndex)
                    {
                        trivalNode.Value.SetDisplay(true);
                        trivalNode.Value.UpdateItemView(itemDatas[i]);
                        if (!bMoveDown)
                        {
                            trivalNode.Value.ShowMoveDown(GetItemPosition(i), 1, 0, delegate
                            {
                                --iMoveCount;
                                if (iMoveCount == 0)
                                {
                                    DoCallback();
                                }
                            });
                        }
                        else
                        {
                            trivalNode.Value.SetItemPosition(GetItemPosition(i));
                            --iMoveCount;
                            if (iMoveCount == 0)
                            {
                                DoCallback();
                            }
                        }
                    }
                    else if (i == iInsertIndex)
                    {
                        trivalNode.Value.SetDisplay(true);
                        trivalNode.Value.UpdateItemView(objItemData);
                        trivalNode.Value.ShowIn(GetItemPosition(iInsertIndex), 1, delegate
                        {
                            --iMoveCount;

                            if (iMoveCount == 0)
                            {
                                DoCallback();
                            }
                        });
                    }
                    else
                    {
                        --iMoveCount;
                        if (iMoveCount == 0)
                        {
                            DoCallback();
                        }
                    }

                    trivalNode = GetCircularlyLinkListNextNode(trivalNode);
                }

                ReSizeContent(fAddItmSize, false);
            }
            else
            {
                Debug.LogError("超出插入範圍，請使用ADD");
                DoCallback();
            }
        }

        public void UpdateIttemData(int itemIndex, object data)
        {
            if (itemIndex < itemDatas.Count && itemIndex >= 0)
            {
                itemDatas[itemIndex] = data;
                CountAllItemsPosition();
                if (itemIndex >= topDataIndex && itemIndex <= bottomDataIndex)
                {
                    var trivalNode = topMenuItem;
                    for (var i = topDataIndex; i <= bottomDataIndex; ++i)
                    {
                        if (i == itemIndex)
                        {
                            trivalNode.Value.SetDisplay(true);
                            trivalNode.Value.UpdateItemView(data);
                            trivalNode.Value.SetItemPosition(GetItemPosition(i));
                        }
                        else
                        {
                            trivalNode.Value.SetItemPosition(GetItemPosition(i));
                        }

                        trivalNode = GetCircularlyLinkListNextNode(trivalNode);
                    }
                }

                ReSizeContent(contentSizeByData, true);
            }
        }

    #endregion

    #region 內部函數

        private void ReSizeContent(float fFixSize, bool bReset)
        {
            scrollRect.enabled = false;
            if (bReset)
            {
                contentSizeInView = fFixSize;
            }
            else
            {
                contentSizeInView += fFixSize;
            }

            menuContentRect.sizeDelta = menuContentRect.sizeDelta * new Vector2(1.0f - (int) direction, 1.0f - (1 - (int) direction)) +
                                        new Vector2(contentSizeInView * (int) direction, contentSizeInView * (1 - (int) direction));
            scrollRect.enabled = true;
            if (contentSizeInView != contentSizeByData)
            {
                Debug.LogWarning("m_fContentSizeInView != m_fDataContentSize");
                Debug.LogWarning("m_fContentSizeInView = " + contentSizeInView + " , m_fDataContentSize = " +
                                 contentSizeByData);
            }
        }

        private void OnScrollRectValueChanged(Vector2 changedValue)
        {
            if(itemDatas==null) return;
            currentContentTop = -(menuContentRect.anchoredPosition.y * (1 - (int) direction) - menuContentRect.anchoredPosition.x * (int) direction);
            currentContentBottom = currentContentTop - scrollRectSize;

            while (currentContentTop > topBoundaryLine)
            {
                //Debug.Log("UP");
                if (topDataIndex > 0)
                {
                    --topDataIndex;
                    object objRemoveItemData = null;
                    if (bottomDataIndex - topDataIndex >= itemNum)
                    {
                        objRemoveItemData = itemDatas[bottomDataIndex];
                        --bottomDataIndex;
                    }

                    bottomMenuItem = GetCircularlyLinkListPreviousNode(topMenuItem);
                    var objAddItemData = itemDatas[topDataIndex];
                    bottomMenuItem.Value.SetDisplay(true);
                    bottomMenuItem.Value.UpdateItemView(objAddItemData);
                    bottomMenuItem.Value.ShowIn(GetItemPosition(topDataIndex), 0, null);
                    topMenuItem = bottomMenuItem;
                    var moveBottom = objRemoveItemData != null ? lowestItemBase.GetItemSize(objRemoveItemData) : 0;
                    MoveTopAndBottomLines(lowestItemBase.GetItemSize(objAddItemData), moveBottom);
                }
                else
                {
                    break;
                }
            }

            while (currentContentBottom < bottomBoundaryLine)
            {
                //Debug.Log("Move Down");
                if (bottomDataIndex < itemDatas.Count - 1)
                {
                    ++bottomDataIndex;
                    object objRemoveItemData = null;
                    if (bottomDataIndex - topDataIndex >= itemNum)
                    {
                        objRemoveItemData = itemDatas[topDataIndex];
                        ++topDataIndex;
                    }

                    var objAddItemData = itemDatas[bottomDataIndex];
                    topMenuItem.Value.SetDisplay(true);
                    topMenuItem.Value.UpdateItemView(itemDatas[bottomDataIndex]);
                    topMenuItem.Value.ShowIn(GetItemPosition(bottomDataIndex), 0, null);
                    topMenuItem = GetCircularlyLinkListNextNode(topMenuItem);
                    var topMove = objRemoveItemData != null ? -lowestItemBase.GetItemSize(objRemoveItemData) : 0;
                    MoveTopAndBottomLines(topMove, -lowestItemBase.GetItemSize(objAddItemData));
                }
                else
                {
                    break;
                }
            }
        }

        private void CountAllItemsPosition()
        {
            float fSize = 0;
            itemPositions = new List<Vector2>();
            foreach (var itemData in itemDatas)
            {
                itemPositions.Add(new Vector2(fSize * (int) direction, -fSize * (1 - (int) direction)));
                fSize += lowestItemBase.GetItemSize(itemData);
            }

            contentSizeByData = fSize;
        }

        private Vector2 GetNewItemPosition()
        {
            return new Vector2(contentSizeByData * (int) direction, -contentSizeByData * (1 - (int) direction));
        }

        private Vector2 GetItemPosition(int iDataIdx)
        {
            return itemPositions[iDataIdx];
        }

        private void MoveTopAndBottomLines(float fTopMove, float fBottomMove)
        {
            //Debug.Log("moveTop = "+ _fTopMove);
            //Debug.Log("moveBottom = " + _fBottomMove);
            topBoundaryLine += fTopMove;
            bottomBoundaryLine += fBottomMove;
            //##Test
            //TopLine.rectTransform.anchoredPosition = new Vector2(0, m_fTopLine);
            //BotLine.rectTransform.anchoredPosition = new Vector2(0, m_fBottomLine);
            //Debug.Log("resetTopAndBottomLines = " + m_fTopLine + "," + m_fBottomLine);
        }

        private void ResetTopAndBottomLines()
        {
            if (itemDatas == null)
            {
                topBoundaryLine = 0;
                bottomBoundaryLine = 0;
            }
            else
            {
                topBoundaryLine = 5.0f;
  

                bottomBoundaryLine = 0.0f;
                var i = 0;
                foreach (var itemData in itemDatas)
                {
                    if (i < itemNum)
                    {
                        bottomBoundaryLine -= lowestItemBase.GetItemSize(itemData);
                    }

                    ++i;
                }
                bottomBoundaryLine -= 5.0f;

            }
            //##Test
            //TopLine.rectTransform.anchoredPosition = new Vector2(0, m_fTopLine);
            //BotLine.rectTransform.anchoredPosition = new Vector2(0, m_fBottomLine);
            //Debug.Log("resetTopAndBottomLines = " + m_fTopLine + ","+ m_fBottomLine);
        }

        private void SetAllItemLock(bool bLock)
        {
            foreach (var item in MenuItems)
            {
                item.LockInteractable(bLock);
            }
        }

        private void DoCallback()
        {
            //Debug.Log("doCallback");
            SetAllItemLock(false);
            if (endCallback != null)
            {
                endCallback.Invoke();
            }
        }

    #endregion

    #region CircularlyLinkList

        private LinkedListNode<MenuItemBase> GetCircularlyLinkListNextNode(LinkedListNode<MenuItemBase> nowNode)
        {
            if (nowNode.Next != null)
            {
                return nowNode.Next;
            }

            return MenuItems.First;
        }

        private LinkedListNode<MenuItemBase> GetCircularlyLinkListPreviousNode(LinkedListNode<MenuItemBase> nowNode)
        {
            if (nowNode.Previous != null)
            {
                return nowNode.Previous;
            }

            return MenuItems.Last;
        }

    #endregion


    //#region 測試用

    //    public void FixedUpdate()
    //    {
    //        Vector2 directionUnit = new Vector3((int) direction, (1 - (int) direction));
    //        Vector2 invDirectionUnit = new Vector3((1 - (int) direction),(int) direction);
    //        var parent = menuContentRect.parent;
    //        int directionFix = (direction==EDirection.VerticalUp2Down)?1:-1;
    //        Vector3 topLineMiddle = parent.TransformPoint( menuContentRect.anchoredPosition + (topBoundaryLine * directionUnit * directionFix));
            
    //        Debug.DrawLine(topLineMiddle+(Vector3)(invDirectionUnit), topLineMiddle-(Vector3)(invDirectionUnit), Color.yellow);
            
    //        Vector3 bottomLineMiddle = parent.TransformPoint( menuContentRect.anchoredPosition+(bottomBoundaryLine * directionUnit * directionFix) );
            
    //        Debug.DrawLine(bottomLineMiddle+(Vector3)(invDirectionUnit), bottomLineMiddle-(Vector3)(invDirectionUnit), Color.green);
    //    }
    //    //[SerializeField] private Image TopLine = null;
    //    //[SerializeField] private Image BotLine = null;


    //    [SerializeField] private int iTest;
    //    private readonly int addCount = 0;
    //    [SerializeField] private int addTestItemsCount;
    //    private void Update()
    //    {
    //        if (Input.GetKeyDown(KeyCode.W))
    //        {
    //            InitTest();
    //        }

    //        if (Input.GetKeyDown(KeyCode.Q))
    //        {
    //            if (itemDatas == null)
    //            {
    //                InitTest();
    //            }

    //            AddTestItem();
    //        }
            
    //        if (Input.GetKeyDown(KeyCode.A))
    //        {
    //            if (itemDatas == null)
    //            {
    //                InitTest();
    //            }

    //            AddTestItems();
    //        }
    //    }

    //    private void InitTest()
    //    {
    //        var l = new List<object>();
    //        for (var i = 0; i < iTest; i++)
    //        {
    //            l.Add(new TestItemData(i, (i % 5 + 1) * 80.0f));
    //        }

    //        SetInitMenuData(l, null);
    //    }

    //    private void AddTestItem()
    //    {
    //        var iNew = iTest++ + addCount;
    //        AddItem(new TestItemData(iNew, (iNew % 5 + 1) * 80.0f), null);
    //        ViewNormalizedPosition = 0;
    //    }
        
    //    private void AddTestItems()
    //    {
    //        var l = new List<object>();
    //        for (var i = 0; i < addTestItemsCount; i++)
    //        {
    //            l.Add(new TestItemData(i, (i % 5 + 1) * 80.0f));
    //        }
    //        AddItems(l,null);
    //        ViewNormalizedPosition = 1;
    //    }

    //#endregion
    }
}