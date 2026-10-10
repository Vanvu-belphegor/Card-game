using System;
using System.Collections.Generic;
using UnityEngine;
using MaxyGames.UNode.Nodes;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditorInternal;
using System.IO;
using System.Reflection;

namespace MaxyGames.UNode.Editors {
	public static class BookmarkManager {
		public static Bookmark bookmark => BookmarkSingleton.instance.bookmark;
		public static event Action OnBookmarkSaved;

		public static IEnumerable<Bookmark> GetBookmarks() {
			return bookmark.GetObjectsInChildren(true, true);
		}

		public static void AddBookmark(Bookmark bookmark) {
			BookmarkManager.bookmark.AddChild(bookmark);
			SaveBookmarks();
		}

		public static void RemoveBookmark(Bookmark bookmark) {
			bookmark.RemoveFromHierarchy();
			SaveBookmarks();
		}

		public static void ClearBookmark() {
			foreach(var v in bookmark.children.ToArray()) {
				v.RemoveFromHierarchy();
			}
			SaveBookmarks();
		}

		public static void SaveBookmarks() {
			BookmarkSingleton.instance.Save();
			OnBookmarkSaved?.Invoke();
			//uNodeEditorUtility.SaveEditorData(_bookmarks.Value, "bookmarks");
		}
	}
}
