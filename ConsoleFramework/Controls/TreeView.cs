﻿using System;
using System.Collections.Generic;
using System.Linq;
using Binding.Observables;
using ConsoleFramework.Core;
using ConsoleFramework.Events;
using Xaml;

namespace ConsoleFramework.Controls
{
    public interface IItemsSource
    {
        IList< TreeItem > GetItems( );
    }

    [ContentProperty("Items")]
    public class TreeItem
    {
        /// <summary>
        /// Pos in TreeView listbox.
        /// </summary>
        internal int Position;

        internal int Level;

        internal String GetDisplayTitle( ) {
            if ( Items.Count != 0 ) {
                return new string(' ', Level * 2) + (Expanded ? UnicodeTable.ArrowDown : UnicodeTable.ArrowRight) + " " + Title;
            } else {
                return new string(' ', Level * 2) + "  " + Title;
            }
        }

        // todo : call listBox.Invalidate() if item is visible now
        public String Title { get; set; }

        private bool disabled;
        public bool Disabled {
            get { return disabled; }
            set {
                if (disabled != value) {
                    disabled = value;
                    // todo : как-то прокинуть своё новое состояние в отображающий его ListBox
                    //Focusable = !disabled;
                    //Invalidate();
                }
            }
        }

        internal readonly ObservableList<TreeItem> items = new ObservableList<TreeItem>(
            new List< TreeItem >());

        // todo : handle modifications of this list
        public IList<TreeItem> Items { get { return items; } }

        public bool HasChildren {
            get { return items.Count != 0; }
        }

        public IItemsSource ItemsSource { get; set; }

        public bool Expanded { get; set; }
    }

    [ContentProperty("Items")]
    public class TreeView : Control
    {
        private readonly ObservableList< TreeItem > items = new ObservableList< TreeItem >(
            new List< TreeItem >( ) );
        
        public IList<TreeItem> Items {
            get { return items; }
        }

        public IItemsSource ItemsSource { get; set; }

        private readonly ListBox listBox;

        public TreeItem SelectedItem {
            get {
                return treeItemsFlat[listBox.SelectedItemIndex];
            }
        }

        public TreeView( ) {
            listBox = new ListBox( );
            listBox.HorizontalAlignment = HorizontalAlignment.Stretch;
            listBox.VerticalAlignment = VerticalAlignment.Stretch;
            
            // Stretch by default too
            this.HorizontalAlignment = HorizontalAlignment.Stretch;
            this.VerticalAlignment = VerticalAlignment.Stretch;

            this.AddChild( listBox );
            this.items.ListChanged += ItemsOnListChanged;

            this.AddHandler( MouseDownEvent, new MouseEventHandler(( sender, args ) => {
                if ( args.Handled ) {
                    expandCollapse(treeItemsFlat[ listBox.SelectedItemIndex ]);
                }
            }), true );

            listBox.SelectedItemIndexChanged += (sender, args) => {
                this.RaisePropertyChanged("SelectedItem");
            };
        }

        private static void subscribeToListChanged(ObservableList<TreeItem> items, ListChangedHandler handler) {
            items.ListChanged += handler;
            foreach (TreeItem item in items) {
                subscribeToListChanged(item.items, handler);
            }
        }

        private static void unsubscribeFromListChanged(ObservableList<TreeItem> items, ListChangedHandler handler) {
            items.ListChanged -= handler;
            foreach (TreeItem item in items) {
                unsubscribeFromListChanged(item.items, handler);
            }
        }

        private void ItemsOnListChanged(object sender, ListChangedEventArgs args) {
            switch (args.Type) {
                case ListChangedEventType.ItemsInserted: {
                        for (int i = 0; i < args.Count; i++) {
                            TreeItem treeItem = this.items[i + args.Index];
                            TreeItem prevItem = null;
                            if (i + args.Index - 1 >= 0)
                                prevItem = this.items[i + args.Index - 1];
                            treeItem.Position = prevItem != null ? prevItem.Position : 0;
                            for (int j = treeItem.Position; j < treeItemsFlat.Count; j++) {
                                treeItemsFlat[j].Position++;
                            }
                            listBox.Items.Insert(treeItem.Position, treeItem.GetDisplayTitle());
                            if (treeItem.Disabled)
                                listBox.DisabledItemsIndexes.Add(treeItem.Position);
                            treeItemsFlat.Insert(treeItem.Position, treeItem);

                            // Handle modification of inner list recursively
                            subscribeToListChanged(treeItem.items, ItemsOnListChanged);
                        }
                        break;
                    }
                case ListChangedEventType.ItemsRemoved: {
                        foreach (TreeItem treeItem in args.RemovedItems.Cast<TreeItem>()) {
                            if (treeItem.Expanded)
                                collapse(treeItem);
                            treeItemsFlat.RemoveAt(treeItem.Position);
                            listBox.Items.RemoveAt(treeItem.Position);
                            for (int j = treeItem.Position; j < treeItemsFlat.Count; j++) {
                                treeItemsFlat[j].Position--;
                            }

                            // Cleanup event handler recursively
                            unsubscribeFromListChanged(treeItem.items, ItemsOnListChanged);
                        }
                        break;
                    }
                default:
                    // todo : handle other event types
                    throw new NotSupportedException();
            }
        }

        private readonly List<TreeItem> treeItemsFlat = new List< TreeItem >();

        private void expand(TreeItem item) {
            int index = treeItemsFlat.IndexOf(item);
            for (int i = 0; i < item.Items.Count; i++) {
                TreeItem child = item.Items[i];
                treeItemsFlat.Insert(i + index + 1, child);
                child.Position = i + index + 1;
                child.Level = item.Level + 1;

                // Учесть уровень вложенности в title
                listBox.Items.Insert(i + index + 1, child.GetDisplayTitle());
                if (child.Disabled) listBox.DisabledItemsIndexes.Add(i + index + 1);
            }
            for (int k = index + 1 + item.Items.Count; k < treeItemsFlat.Count; k++) {
                treeItemsFlat[k].Position += item.Items.Count;
            }
        }

        private void collapse(TreeItem item) {
            int index = treeItemsFlat.IndexOf(item);
            foreach (TreeItem child in item.Items) {
                treeItemsFlat.RemoveAt(index + 1);
                if (child.Disabled) listBox.DisabledItemsIndexes.Remove(index + 1);
                listBox.Items.RemoveAt(index + 1);
                child.Position = -1;
            }
            for (int k = index + 1; k < treeItemsFlat.Count; k++) {
                treeItemsFlat[k].Position -= item.Items.Count;
            }
        }

        private void expandCollapse( TreeItem item ) {
            int index = treeItemsFlat.IndexOf(item);
            if ( item.Expanded ) {
                // Children are collapsed but with Expanded state saved
                foreach (TreeItem child in item.Items.Where(child => child.Expanded)) {
                    collapse(child);
                }

                collapse(item);
                item.Expanded = false;
                // Need to update item string (because Expanded status has been changed)
                listBox.Items[index] = item.GetDisplayTitle();
            } else {
                expand(item);
                item.Expanded = true;
                // Need to update item string (because Expanded status has been changed)
                listBox.Items[index] = item.GetDisplayTitle();

                // Children are expanded too according to their Expanded stored state
                foreach (TreeItem child in item.Items.Where(child => child.Expanded)) {
                    expand(child);
                }
            }
        }

        protected override Size MeasureOverride( Size availableSize ) {
            listBox.Measure( availableSize );
            return listBox.DesiredSize;
        }

        protected override Size ArrangeOverride( Size finalSize ) {
            listBox.Arrange( new Rect(finalSize) );
            return finalSize;
        }
    }
}
