#pragma warning disable CS0618
using System;
using System.Collections.Generic;
using UnityEngine;
using MaxyGames.UNode.Nodes;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;


namespace MaxyGames.UNode.Editors {
	public class BookmarkWindow : EditorWindow {
		private BookmarkTreeView manager;
		private SearchField searchField;
		private Vector2 scrollPos;
		private string searchText;
		private string m_searchTextTemp;
		[SerializeReference]
		private MultiColumnHeaderState multiColumnState;

		public static void ShowWindow() {
			BookmarkWindow window = (BookmarkWindow)GetWindow(typeof(BookmarkWindow));
			window.autoRepaintOnSceneChange = true;
			window.wantsMouseMove = true;
			window.titleContent = new GUIContent("Bookmarks");
			window.Show();
		}

		private void OnEnable() {
			searchField = new SearchField();
			if(multiColumnState == null) {
				multiColumnState = new MultiColumnHeaderState(
					new[] {
					new MultiColumnHeaderState.Column() {
						headerContent = new GUIContent(EditorGUIUtility.FindTexture("FilterByType")),
						contextMenuText = "Icon",
						headerTextAlignment = TextAlignment.Center,
						sortedAscending = true,
						sortingArrowAlignment = TextAlignment.Right,
						width = 30,
						minWidth = 30,
						maxWidth = 60,
						autoResize = false,
						allowToggleVisibility = false
					},
					new MultiColumnHeaderState.Column() {
						headerContent = new GUIContent("Name"),
						headerTextAlignment = TextAlignment.Left,
						sortedAscending = true,
						sortingArrowAlignment = TextAlignment.Center,
						width = 150,
						minWidth = 60,
						autoResize = false,
						allowToggleVisibility = false
					},
					new MultiColumnHeaderState.Column() {
						headerContent = new GUIContent("Location"),
						headerTextAlignment = TextAlignment.Left,
						sortedAscending = true,
						sortingArrowAlignment = TextAlignment.Center,
						width = 300,
						minWidth = 60,
						autoResize = false,
						allowToggleVisibility = false
					},
					});
			}
			var multiColumn = new MultiColumnHeader(multiColumnState);
			multiColumn.canSort = false;
			multiColumn.ResizeToFit();
			manager = new(multiColumn);
			BookmarkManager.OnBookmarkSaved -= OnBookmarkSaved;
			BookmarkManager.OnBookmarkSaved += OnBookmarkSaved;
		}

		private void OnDisable() {
			BookmarkManager.OnBookmarkSaved -= OnBookmarkSaved;
		}

		void OnBookmarkSaved() {
			uNodeThreadUtility.ExecuteOnce(() => {
				manager.Reload();
			}, "BOOKMARK_WINDOW");
		}

		private void OnGUI() {
			DrawToolbar();
			scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
			if(manager != null) {
				manager.OnGUI(GUILayoutUtility.GetRect(0, 100000, 0, 100000));
			}
			EditorGUILayout.EndScrollView();
		}

		void DrawToolbar() {
			using(new GUILayout.HorizontalScope()) {
				var search = searchField.OnToolbarGUI(m_searchTextTemp);
				if(search != m_searchTextTemp) {
					m_searchTextTemp = search;
					searchText = search;
					SearchChanged();
				}
				//if(GUILayout.Button("Search")) {
				//	searchText = search;
				//	SearchChanged();
				//	manager.Reload();
				//}
				if(GUILayout.Button("New Group", GUILayout.Width(80))) {
					var data = new BookmarkGroup() {
						name = "newGroup"
					};
					BookmarkManager.AddBookmark(data);
					manager.Reload();
					var tree = manager.FindItem(uNodeUtility.GetHashCode(data.guid));
					if(tree != null) {
						manager.BeginRename(tree);
					}
				}
				if(GUILayout.Button("Refresh", GUILayout.Width(60))) {
					BookmarkManager.SaveBookmarks();
					manager.Reload();
				}
			}
		}

		void SearchChanged() {
			manager.searchString = searchText;
		}
	}

	internal class BookmarkTreeView : TreeView {
		private static readonly string DataKey = "UNODE_BOOKMARK";

		class BookmarkTreeViewItem : TreeViewItem {
			public Bookmark bookmark;

			public object userData;

			public BookmarkTreeViewItem(Bookmark bookmark) : base(uNodeUtility.GetHashCode(bookmark.guid), -1, bookmark.name) {
				this.bookmark = bookmark;
			}
		}

		public BookmarkTreeView(MultiColumnHeader multiColumnHeader = null) : base(new TreeViewState(), multiColumnHeader) {
			showBorder = true;
			showAlternatingRowBackgrounds = true;
			columnIndexForTreeFoldouts = 1;
			Reload();
		}

		protected override bool CanBeParent(TreeViewItem item) {
			if(item is BookmarkTreeViewItem tree) {
				return tree.bookmark is IGroup;
			}
			return false;
		}

		protected override bool CanRename(TreeViewItem item) {
			return false;
		}

		protected override void RenameEnded(RenameEndedArgs args) {
			base.RenameEnded(args);
			var tree = FindItem(args.itemID, rootItem) as BookmarkTreeViewItem;
			if(tree != null) {
				tree.bookmark.name = args.newName;
				Reload();
				BookmarkManager.SaveBookmarks();
			}
		}

		public TreeViewItem FindItem(int id) {
			return FindItem(id, rootItem);
		}

		protected override Rect GetRenameRect(Rect rowRect, int row, TreeViewItem item) {
			rowRect = multiColumnHeader.GetCellRect(1, rowRect);
			return base.GetRenameRect(rowRect, row, item);
		}

		protected override bool CanStartDrag(CanStartDragArgs args) => true;

		protected override void SetupDragAndDrop(SetupDragAndDropArgs args) {
			var selections = args.draggedItemIDs;
			if(selections.Count <= 0)
				return;

			var dragObjects = GetRows()
				.Where(i => selections.Contains(i.id))
				.ToArray()
				;

			if(dragObjects.Length <= 0)
				return;

			DragAndDrop.PrepareStartDrag();
			DragAndDrop.SetGenericData(DataKey, dragObjects);
			DragAndDrop.StartDrag(dragObjects.Length > 1 ? "<Multiple>" : dragObjects[0].displayName);
		}

		protected override DragAndDropVisualMode HandleDragAndDrop(DragAndDropArgs args) {
			if(args.performDrop) {
				var data = DragAndDrop.GetGenericData(DataKey);
				var items = data as TreeViewItem[];

				if(items == null || items.Length <= 0)
					return DragAndDropVisualMode.None;

				var item = items.First() as BookmarkTreeViewItem;
				switch(args.dragAndDropPosition) {
					case DragAndDropPosition.UponItem:
						if(args.parentItem is BookmarkTreeViewItem parent) {
							parent.bookmark.AddChild(item.bookmark);
						}
						SetSelection(new List<int> { item.id });
						Reload();
						break;
					case DragAndDropPosition.BetweenItems:
						if(args.insertAtIndex >= args.parentItem.children.Count) {
							item.bookmark.RemoveFromHierarchy();
							(args.parentItem as BookmarkTreeViewItem).bookmark.AddChild(item.bookmark);
						}
						else {
							var dropItem = args.parentItem.children[args.insertAtIndex] as BookmarkTreeViewItem;
							item.bookmark.SetParent(dropItem.bookmark.parent);
							item.bookmark.PlaceBehind(dropItem.bookmark);
						}
						SetSelection(new List<int> { item.id });
						Reload();
						break;
					case DragAndDropPosition.OutsideItems:
						break;
					default:
						throw new ArgumentOutOfRangeException();
				}
			}
			else if(isDragging) {
			}

			return DragAndDropVisualMode.Move;
		}

		protected override TreeViewItem BuildRoot() {
			var root = new BookmarkTreeViewItem(BookmarkManager.bookmark) { id = 0, depth = -1 };

			foreach(var bookmark in BookmarkManager.bookmark.GetObjectsInChildren()) {
				RecursiveBuildTree(root, bookmark);
			}
			if(root.children == null) {
				root.children = new List<TreeViewItem>();
			}
			SetupDepthsFromParentsAndChildren(root);
			if(oldExpandedIds == null)
				oldExpandedIds = new();
			oldExpandedIds.Clear();
			oldExpandedIds.AddRange(GetExpanded());
			return root;
		}

		void RecursiveBuildTree(TreeViewItem parent, Bookmark bookmark) {
			var tree = new BookmarkTreeViewItem(bookmark);
			SetExpanded(tree.id, bookmark.IsExpanded);
			parent.AddChild(tree);
			foreach(var val in bookmark.GetObjectsInChildren()) {
				RecursiveBuildTree(tree, val);
			}
		}

		private List<int> oldExpandedIds;
		protected override void ExpandedStateChanged() {
			if(oldExpandedIds != null) {
				bool hasChanged = false;
				var expandedIds = GetExpanded();
				foreach(var id in expandedIds) {
					if(oldExpandedIds.Contains(id) == false) {
						//For newers expand state
						var tree = FindItem(id, rootItem) as BookmarkTreeViewItem;
						if(tree != null && tree.bookmark is BookmarkGroup group) {
							group.expanded = true;
							hasChanged = true;
						}
					}
				}
				foreach(var id in oldExpandedIds) {
					if(expandedIds.Contains(id) == false) {
						//For newers colapse state
						var tree = FindItem(id, rootItem) as BookmarkTreeViewItem;
						if(tree != null && tree.bookmark is BookmarkGroup group) {
							group.expanded = false;
							hasChanged = true;
						}
					}
				}
				oldExpandedIds.Clear();
				oldExpandedIds.AddRange(expandedIds);
				if(hasChanged)
					BookmarkManager.SaveBookmarks();
			}
		}

		protected override bool CanMultiSelect(TreeViewItem item) {
			return false;
		}

		private float locationWidth = 0, lastLocationWidth = 0;
		protected override void BeforeRowsGUI() {
			base.BeforeRowsGUI();
			locationWidth = 0;
		}

		protected override void AfterRowsGUI() {
			base.AfterRowsGUI();
			if(locationWidth > 0)
				lastLocationWidth = locationWidth;
		}

		protected override void RowGUI(RowGUIArgs args) {
			if(multiColumnHeader != null) {
				var tree = args.item as BookmarkTreeViewItem;
				var fullRect = args.rowRect;
				{
					//Icon
					args.rowRect = args.GetCellRect(0);
					var icon = uNodeEditorUtility.GetTypeIcon(tree.bookmark);
					GUI.DrawTexture(new Rect(args.rowRect.x + 4, args.rowRect.y, 16, 16), icon);
				}
				{
					//Name
					args.rowRect = args.GetCellRect(1);
					DrawRow(args, fullRect);
				}
				if(Event.current.type == EventType.Repaint) {
					//Location
					args.rowRect = args.GetCellRect(2);
					if(tree.bookmark is BookmarkCanvas canvas) {
						var reference = canvas.canvas.reference;
						if(reference != null) {
							if(tree.userData == null) {
								tree.userData = ErrorCheckWindow.GetElementPathWithIcon(reference, richText: true, true);
							}
							var paths = tree.userData as List<(string, Texture)>;
							var width = GraphUtility.ReferenceTree.DrawReferencePath(args.rowRect, paths);
							if(width > locationWidth) {
								locationWidth = width;
								multiColumnHeader.GetColumn(2).width = Mathf.Max(width, lastLocationWidth);
							}
						}
					}
					else if(tree.bookmark is BookmarkElement bookmarkElement) {
						var reference = bookmarkElement.element.reference;
						if(reference != null) {
							if(tree.userData == null) {
								tree.userData = ErrorCheckWindow.GetElementPathWithIcon(reference, richText: true, true);
							}
							var paths = tree.userData as List<(string, Texture)>;
							var width = GraphUtility.ReferenceTree.DrawReferencePath(args.rowRect, paths);
							if(width > locationWidth) {
								locationWidth = width;
								multiColumnHeader.GetColumn(2).width = Mathf.Max(width, lastLocationWidth);
							}
						}
					}
				}
			}
			else {
				DrawRow(args, args.rowRect);
			}
		}

		private void DrawRow(RowGUIArgs args, Rect fullRect) {
			Event evt = Event.current;
			if(evt.type == EventType.Repaint) {

			}
			else if(evt.type == EventType.MouseDown && fullRect.Contains(evt.mousePosition)) {
				var tree = args.item as BookmarkTreeViewItem;
				if(evt.button == 1) {
					var mPos = evt.mousePosition.ToScreenPoint();
					GenericMenu menu = new GenericMenu();
					menu.AddItem(new GUIContent("Edit..."), false, () => {
						ActionPopupWindow.Show(() => {
							EditBookmark(tree.bookmark);
						}, onGUIBottom: () => {
							if(GUILayout.Button("Save")) {
								BookmarkManager.SaveBookmarks();
								Reload();
								ActionPopupWindow.CloseAll();
							}
						}).ChangePosition(mPos);
					});
					menu.AddItem(new GUIContent("Rename"), false, () => {
						BeginRename(tree);
					});
					if(tree.bookmark.CanOpen()) {
						menu.AddItem(new GUIContent("Go to bookmark"), false, () => {
							tree.bookmark.OnOpen();
						});
					}

					if(tree.bookmark.CanOpenAsset()) {
						menu.AddItem(new GUIContent("Ping asset"), false, () => {
							tree.bookmark.OnOpenAsset();
						});
					}
					menu.AddSeparator("");
					menu.AddItem(new GUIContent("Remove"), false, () => {
						BookmarkManager.RemoveBookmark(tree.bookmark);
						Reload();
					});
					menu.ShowAsContext();
				}
				else if(evt.button == 0 && evt.clickCount == 2) {
					if(tree.bookmark.CanOpen()) {
						tree.bookmark.OnOpen();
					}
					else {
						BeginRename(tree);
					}
				}
			}
			base.RowGUI(args);
		}

		private void EditBookmark(Bookmark bookmark) {
			uNodeGUIUtility.ShowFields(bookmark);
		}
	}
}
