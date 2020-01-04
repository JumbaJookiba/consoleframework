﻿using ConsoleFramework.Controls;
using ConsoleFramework.Core;
using Xunit;

namespace Tests
{
    public sealed class LayoutTests
    {
        private class TestContentControl : Control
        {
            public Control Content {
                get;
                set;
            }

            protected override Size MeasureOverride(Size availableSize) {
                LastMeasureOverrideArgument = availableSize;
                Size res;
                if (null != Content) {
                    Content.Measure(availableSize);
                    res = Content.DesiredSize;
                } else {
                    res = base.MeasureOverride(availableSize);
                }
                LastMeasureOverrideResult = res;
                return res;
            }

            protected override Size ArrangeOverride(Size finalSize) {
                LastArrangeOverrideArgument = finalSize;
                LastArrangeOverrideResult = base.ArrangeOverride(finalSize);
                if (null != Content) {
                    Content.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));
                }
                return LastArrangeOverrideResult.Value;
            }

            public Size? LastMeasureOverrideArgument {
                get;
                private set;
            }

            public Size? LastMeasureOverrideResult {
                get;
                set;
            }

            public Size? LastArrangeOverrideArgument {
                get;
                private set;
            }

            public Size? LastArrangeOverrideResult {
                get;
                private set;
            }
        }

        private class TestFinalControl : Control {
            protected override Size MeasureOverride(Size availableSize) {
                LastMeasureOverrideArgument = availableSize;
                Size res = base.MeasureOverride(availableSize);
                LastMeasureOverrideResult = res;
                return res;
            }

            protected override Size ArrangeOverride(Size finalSize) {
                LastArrangeOverrideArgument = finalSize;
                LastArrangeOverrideResult = base.ArrangeOverride(finalSize);
                return LastArrangeOverrideResult.Value;
            }

            public Size? LastMeasureOverrideArgument {
                get;
                private set;
            }

            public Size? LastMeasureOverrideResult {
                get;
                set;
            }

            public Size? LastArrangeOverrideArgument {
                get;
                private set;
            }

            public Size? LastArrangeOverrideResult {
                get;
                private set;
            }
        }

        [Fact]
        public void TestNormalMeasure() {
            TestContentControl contentControl = new TestContentControl();
            TestFinalControl finalControl = new TestFinalControl {
                Width = 100,
                Height = 100,
                Margin = new Thickness(10, 0, 20, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top
            };
            contentControl.Content = finalControl;
            contentControl.Width = 80;
            contentControl.Height = 80;
            contentControl.Measure(new Size(80, 80));
            contentControl.Arrange(new Rect(0, 0, 80, 80));
            //
            Assert.Equal(new Rect(0, 0, 80, 80), finalControl.RenderSlotRect);
            Assert.Equal(new Vector(-15, 0), finalControl.ActualOffset);
            Assert.Equal(new Size(100, 100), finalControl.RenderSize);
            Assert.Equal(new Size(80, 80), finalControl.DesiredSize);
            Assert.Equal(new Rect(25, 0, 50, 80), finalControl.LayoutClip);
            Assert.Equal(new Size(80, 80), finalControl.MeasureArgument);
            Assert.Equal(new Size(100, 100), finalControl.LastMeasureOverrideArgument); // because Width/Height
            Assert.Equal(new Size(0, 0), finalControl.LastMeasureOverrideResult);
            Assert.Equal(new Size(100, 100), finalControl.LastArrangeOverrideArgument);
            Assert.Equal(new Size(100, 100), finalControl.LastArrangeOverrideResult);
        }

        [Fact]
        public void TestNormalMeasure2() {
            TestContentControl contentControl = new TestContentControl();
            TestFinalControl finalControl = new TestFinalControl {
                Width = 100,
                Height = 20,
                Margin = new Thickness(10, 0, 20, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top
            };
            contentControl.Content = finalControl;
            // do not set contentControl.Width and Height explicitly
            contentControl.Measure(new Size(80, 80));
            contentControl.Arrange(new Rect(0, 0, 80, 80));
            //
            Assert.Equal(new Rect(0, 0, 80, 20), finalControl.RenderSlotRect);
            Assert.Equal(new Vector(-15, 0), finalControl.ActualOffset);
            Assert.Equal(new Size(100, 20), finalControl.RenderSize);
            Assert.Equal(new Size(80, 20), finalControl.DesiredSize);
            Assert.Equal(new Rect(25, 0, 50, 20), finalControl.LayoutClip);
            Assert.Equal(new Size(80, 80), finalControl.MeasureArgument);
            Assert.Equal(new Size(100, 20), finalControl.LastMeasureOverrideArgument); // because Width/Height
            Assert.Equal(new Size(0, 0), finalControl.LastMeasureOverrideResult);
            Assert.Equal(new Size(100, 20), finalControl.LastArrangeOverrideArgument);
            Assert.Equal(new Size(100, 20), finalControl.LastArrangeOverrideResult);
        }

        [Fact]
        public void TestMaxIntWithMarginMeasure() {
            var contentControl = new TestContentControl();
            var finalControl = new TestFinalControl() {
                Margin = new Thickness(1)
            };
            contentControl.Content = finalControl;
            contentControl.Measure(new Size(int.MaxValue, int.MaxValue));
            
            // Margin should not reduce measure argument by 1,
            // MaxValue should be treated like PositiveInf in doubles arithmetic
            Assert.Equal(new Size(int.MaxValue, int.MaxValue), finalControl.LastMeasureOverrideArgument);
            Assert.Equal(new Size(0, 0), finalControl.LastMeasureOverrideResult);
        }
        
        [Fact]
        public void TestNormalMeasure3() {
            TestContentControl contentControl = new TestContentControl();
            TestFinalControl finalControl = new TestFinalControl {
                Width = 100,
                Height = 20,
                Margin = new Thickness(10, -10, 20, 7),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            contentControl.Content = finalControl;
            contentControl.Width = 70;
            contentControl.Height = 80;
            contentControl.Measure(new Size(1000, 1000));
            contentControl.Arrange(new Rect(0, 0, 1000, 1000));
            //
            Assert.Equal(new Rect(0, 0, 70, 80), finalControl.RenderSlotRect);
            Assert.Equal(new Vector(-50, 21), finalControl.ActualOffset);
            Assert.Equal(new Size(100, 20), finalControl.RenderSize);
            Assert.Equal(new Size(70, 17), finalControl.DesiredSize);
            Assert.Equal(new Rect(60, 0, 40, 20), finalControl.LayoutClip);
            Assert.Equal(new Size(70, 80), finalControl.MeasureArgument);
            Assert.Equal(new Size(100, 20), finalControl.LastMeasureOverrideArgument); // because Width/Height
            Assert.Equal(new Size(0, 0), finalControl.LastMeasureOverrideResult);
            Assert.Equal(new Size(100, 20), finalControl.LastArrangeOverrideArgument);
            Assert.Equal(new Size(100, 20), finalControl.LastArrangeOverrideResult);
        }
    }
}
