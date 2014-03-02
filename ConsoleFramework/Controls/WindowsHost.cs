﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using ConsoleFramework.Core;
using ConsoleFramework.Events;
using ConsoleFramework.Native;
using ConsoleFramework.Rendering;

namespace ConsoleFramework.Controls
{
    /// <summary>
    /// Класс, служащий хост-панелью для набора перекрывающихся окон.
    /// Хранит в себе список окон в порядке их Z-Order и отрисовывает рамки,
    /// управляет их перемещением.
    /// </summary>
    public class WindowsHost : Control
    {
        private Menu mainMenu;
        public Menu MainMenu
        {
            get { return mainMenu; }
            set {
                if ( mainMenu != value ) {
                    if ( mainMenu != null ) {
                        RemoveChild( mainMenu );
                    }
                    if ( value != null ) {
                        InsertChildAt(0, value);
                    }
                    mainMenu = value;
                }
            }
        }

        public WindowsHost() {
            AddHandler(PreviewMouseDownEvent, new MouseButtonEventHandler(OnPreviewMouseDown), true);
            AddHandler( PreviewMouseMoveEvent, new MouseEventHandler(onPreviewMouseMove), true );
            AddHandler( MouseMoveEvent, new MouseEventHandler(( sender, args ) => {
                //Debugger.Log( 1, "", "WindowHost.MouseMove\n" );
            }) );
        }

        private void onPreviewMouseMove( object sender, MouseEventArgs args ) {
            if ( args.LeftButton == MouseButtonState.Pressed ) {
                OnPreviewMouseDown( sender, args );
            }
        }

        protected override Size MeasureOverride(Size availableSize) {
            int windowsStartIndex = 0;
            if ( mainMenu != null ) {
                assert( Children[ 0 ] == mainMenu );
                mainMenu.Measure( new Size(availableSize.Width, 1) );
                windowsStartIndex++;
            }

            // Дочерние окна могут занимать сколько угодно пространства,
            // но при заданных Width/Height их размеры будут учтены
            // системой размещения автоматически
            for ( int index = windowsStartIndex; index < Children.Count; index++ ) {
                Control control = Children[ index ];
                Window window = ( Window ) control;
                window.Measure( new Size( int.MaxValue, int.MaxValue ) );
            }
            return availableSize;
        }

        protected override Size ArrangeOverride(Size finalSize) {
            int windowsStartIndex = 0;
            if ( mainMenu != null ) {
                assert( Children[ 0 ] == mainMenu );
                mainMenu.Arrange( new Rect(0, 0, finalSize.Width, 1) );
                windowsStartIndex++;
            }
            // сколько дочерние окна хотели - столько и получают
            for ( int index = windowsStartIndex; index < Children.Count; index++ ) {
                Control control = Children[ index ];
                Window window = ( Window ) control;
                int x;
                if ( window.X.HasValue ) {
                    x = window.X.Value;
                } else {
                    x = ( finalSize.Width - window.DesiredSize.Width )/2;
                }
                int y;
                if ( window.Y.HasValue ) {
                    y = window.Y.Value;
                } else {
                    y = ( finalSize.Height - window.DesiredSize.Height )/2;
                }
                window.Arrange( new Rect( x, y, window.DesiredSize.Width, window.DesiredSize.Height ) );
            }
            return finalSize;
        }

        public override void Render(RenderingBuffer buffer)
        {
            buffer.FillRectangle(0, 0, ActualWidth, ActualHeight, ' ', Attr.BACKGROUND_BLUE);
        }

        /// <summary>
        /// Делает указанное окно активным. Если оно до этого не было активным, то
        /// по Z-индексу оно будет перемещено на самый верх, и получит клавиатурный фокус ввода.
        /// </summary>
        private void activateWindow(Window window) {
            int index = Children.FindIndex(0, control => control == window);
            if (-1 == index)
                throw new InvalidOperationException("Assertion failed.");
            //
            Control oldTopWindow = Children[Children.Count - 1];
            for (int i = index; i < Children.Count - 1; i++) {
                Children[i] = Children[i + 1];
            }
            Children[Children.Count - 1] = window;
            
            if (oldTopWindow != window)
            {
                oldTopWindow.RaiseEvent( Window.DeactivatedEvent, new RoutedEventArgs( oldTopWindow, Window.DeactivatedEvent ) );
                window.RaiseEvent(Window.ActivatedEvent, new RoutedEventArgs(window, Window.ActivatedEvent));
                initializeFocusOnActivatedWindow( window );
                Invalidate();
            }
        }
        
        private bool isTopWindowModal( ) {
            int windowsStartIndex = 0;
            if ( mainMenu != null ) {
                assert( Children[ 0 ] == mainMenu );
                windowsStartIndex++;
            }

            if ( Children.Count == windowsStartIndex ) return false;
            return windowInfos[ (Window) Children[ Children.Count - 1 ] ].Modal;
        }

        /// <summary>
        /// Обработчик отвечает за вывод на передний план неактивных окон, на которые нажали мышкой,
        /// и за обработку мыши, когда имеется модальное окно - в этом случае обработчик не пропускает
        /// события, которые идут мимо модального окна, дальше по дереву (Tunneling) - устанавливая
        /// Handled в True, либо закрывает модальное окно, если оно было показано с флагом
        /// OutsideClickClosesWindow.
        /// </summary>
        public void OnPreviewMouseDown(object sender, MouseEventArgs args) {
            bool handle = false;
            if ( isTopWindowModal( ) ) {
                Window modalWindow = ( Window ) Children[ Children.Count - 1 ];
                Window windowClicked = VisualTreeHelper.FindClosestParent<Window>((Control)args.Source);
                if ( windowClicked != modalWindow ) {
                    if ( windowInfos[ modalWindow ].OutsideClickClosesWindow ) {
                        // закрываем текущее модальное окно
                        CloseWindow( modalWindow );

                        // далее обрабатываем событие как обычно
                        handle = true;
                    } else {
                        // прекращаем распространение события (правда, контролы, подписавшиеся с флагом
                        // handledEventsToo, получат его в любом случае) и генерацию соответствующего
                        // парного не-preview события
                        args.Handled = true;
                    }
                }
            } else {
                handle = true;
            }
            if (handle) {
                Window windowClicked = VisualTreeHelper.FindClosestParent< Window >( ( Control ) args.Source );
                if ( null != windowClicked ) {
                    activateWindow( windowClicked );
                } else {
                    Menu menu = VisualTreeHelper.FindClosestParent< Menu >( ( Control ) args.Source );
                    if ( null != menu ) {
                        activateMenu(  );
                    }
                }
            }
        }

        private void activateMenu( ) {
            assert( mainMenu != null );
            if (ConsoleApplication.Instance.FocusManager.CurrentScope != mainMenu)
                ConsoleApplication.Instance.FocusManager.SetFocusScope( mainMenu );
        }

        private void initializeFocusOnActivatedWindow( Window window ) {
            bool reinitFocus = false;
            if ( window.StoredFocus != null ) {
                // проверяем, не удалён ли StoredFocus и является ли он Visible & Focusable
                if ( !VisualTreeHelper.IsConnectedToRoot( window.StoredFocus ) ) {
                    // todo : log warn about disconnected control
                    reinitFocus = true;
                } else if ( window.StoredFocus.Visibility != Visibility.Visible ) {
                    // todo : log warn about invizible control to be focused
                    reinitFocus = true;
                }
                else if ( !window.StoredFocus.Focusable ) {
                    // todo : log warn
                    reinitFocus = true;
                } else {
                    ConsoleApplication.Instance.FocusManager.SetFocus( window, window.StoredFocus );
                }
            } else {
                reinitFocus = true;
            }
            //
            if ( reinitFocus ) {
                if ( window.ChildToFocus != null ) {
                    Control child = VisualTreeHelper.FindChildByName( window, window.ChildToFocus );
                    ConsoleApplication.Instance.FocusManager.SetFocus( child );
                } else {
                    ConsoleApplication.Instance.FocusManager.SetFocusScope( window );
                }
            }
        }

        private class WindowInfo
        {
            public readonly bool Modal;
            public readonly bool OutsideClickClosesWindow;

            public WindowInfo( bool modal, bool outsideClickClosesWindow ) {
                Modal = modal;
                OutsideClickClosesWindow = outsideClickClosesWindow;
            }
        }

        private readonly Dictionary<Window, WindowInfo> windowInfos = new Dictionary< Window, WindowInfo >();

        /// <summary>
        /// Adds window to window host children and shows it as modal window.
        /// </summary>
        public void ShowModal( Window window, bool outsideClickWillCloseWindow = false ) {
            showCore( window, true, outsideClickWillCloseWindow );
        }

        /// <summary>
        /// Adds window to window host children and shows it.
        /// </summary>
        public void Show(Window window) {
            showCore( window, false, false );
        }

        private Window getTopWindow( ) {
            int windowsStartIndex = 0;
            if ( mainMenu != null ) {
                assert( Children[ 0 ] == mainMenu );
                windowsStartIndex++;
            }
            if ( Children.Count > windowsStartIndex ) {
                return ( Window ) Children[ Children.Count - 1 ];
            }
            return null;
        }

        private void showCore( Window window, bool modal, bool outsideClickWillCloseWindow ) {
            Control topWindow = getTopWindow(  );
            if ( null != topWindow ) {
                topWindow.RaiseEvent( Window.DeactivatedEvent,
                                        new RoutedEventArgs( topWindow, Window.DeactivatedEvent ) );
            }

            AddChild(window);
            window.RaiseEvent( Window.ActivatedEvent, new RoutedEventArgs( window, Window.ActivatedEvent ) );
            initializeFocusOnActivatedWindow(window);
            windowInfos.Add( window, new WindowInfo( modal, outsideClickWillCloseWindow ) );
        }

        /// <summary>
        /// Removes window from window host.
        /// </summary>
        public void CloseWindow(Window window) {
            windowInfos.Remove( window );
            window.RaiseEvent( Window.DeactivatedEvent, new RoutedEventArgs( window, Window.DeactivatedEvent ) );
            RemoveChild(window);
            window.RaiseEvent( Window.ClosedEvent, new RoutedEventArgs( window, Window.ClosedEvent ) );
            // после удаления окна активизировать то, которое было активным до него
            List<Control> childrenOrderedByZIndex = GetChildrenOrderedByZIndex();

            int windowsStartIndex = 0;
            if ( mainMenu != null ) {
                assert( Children[ 0 ] == mainMenu );
                windowsStartIndex++;
            }

            if ( childrenOrderedByZIndex.Count > windowsStartIndex ) {
                Window topWindow = ( Window ) childrenOrderedByZIndex[ childrenOrderedByZIndex.Count - 1 ];
                topWindow.RaiseEvent( Window.ActivatedEvent, new RoutedEventArgs( topWindow, Window.ActivatedEvent ) );
                initializeFocusOnActivatedWindow(topWindow);
                Invalidate();
            }
        }

        /// <summary>
        /// Утилитный метод, позволяющий для любого контрола определить ближайший WindowsHost,
        /// в котором он размещается.
        /// </summary>
        public static WindowsHost FindWindowsHostParent( Control control ) {
            Control tmp = control;
            while ( tmp != null && !( tmp is WindowsHost ) ) {
                tmp = tmp.Parent;
            }
            return ( WindowsHost ) ( tmp );
        }
    }
}
