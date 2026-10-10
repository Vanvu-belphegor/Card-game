using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEditor;

namespace MaxyGames.UNode.Editors {
	[Serializable]
    public abstract class Bookmark : UTreeElement<Bookmark>, IIcon {
        public string name;
        public string summary;
		[Hide]
		public string guid = uNodeUtility.GenerateUID();

		public virtual bool IsExpanded => false;

		public virtual bool CanOpen() => false;
		public virtual void OnOpen() { }

		public virtual bool CanOpenAsset() => false;
		public virtual void OnOpenAsset() { }

        public virtual bool IsValid() => true;
        public virtual Type GetIcon() => typeof(TypeIcons.BookmarkIcon);
	}

    [Serializable]
    public class BookmarkGroup : Bookmark, IGroup {
		[Hide]
		public bool expanded = true;

		public override bool IsExpanded => expanded;
		public override Type GetIcon() => typeof(TypeIcons.FolderIcon);
	}

    [Serializable]
    public class BookmarkCanvas : Bookmark {
		[Hide]
		public UGraphElementRef canvas;
		public Texture2D icon;
		public Vector2 position;
		public float zoomScale = 1;

		public override bool IsValid() {
            return canvas.reference != null && canvas.reference.graphContainer != null;
		}

		public override bool CanOpen() {
			return canvas?.reference != null;
		}

		public override void OnOpen() {
			var reference = canvas.reference;
			var win = uNodeEditor.Open(reference.graphContainer, reference);
			win.graphEditor.SetZoomScale(zoomScale);
			win.graphEditor.MoveCanvas(position);
		}

		public override bool CanOpenAsset() {
			return canvas?.reference?.graphContainer != null;
		}

		public override void OnOpenAsset() {
			EditorGUIUtility.PingObject(canvas.reference.graphContainer as UnityEngine.Object);
		}

		public override Type GetIcon() {
			if(canvas.reference == null) {
				return typeof(TypeIcons.MissingIcon);
			}
			if(icon != null) {
				return TypeIcons.FromTexture(icon);
			}
			return base.GetIcon();
		}
	}

    [Serializable]
    public class BookmarkElement : Bookmark {
		[Hide]
        public UGraphElementRef element;
		public Texture2D icon;

		public override bool IsValid() {
			return element.reference != null;
		}

		public override bool CanOpen() {
			return element?.reference != null;
		}

		public override void OnOpen() {
			if(element.reference is NodeObject) {
				uNodeEditor.Highlight(element.reference as NodeObject);
			}
			else {
				uNodeEditor.Open(element.reference.graphContainer, element.reference);
			}
		}

		public override bool CanOpenAsset() {
			return element?.reference?.graphContainer != null;
		}

		public override void OnOpenAsset() {
			EditorGUIUtility.PingObject(element.reference.graphContainer as UnityEngine.Object);
		}

		public override Type GetIcon() {
			if(element.reference == null) {
				return typeof(TypeIcons.MissingIcon);
			}
			if(icon != null) {
				return TypeIcons.FromTexture(icon);
			}
			if(element.reference is IIcon iconRef) {
				return iconRef.GetIcon() ?? base.GetIcon();
			}
			return base.GetIcon();
		}
	}
}