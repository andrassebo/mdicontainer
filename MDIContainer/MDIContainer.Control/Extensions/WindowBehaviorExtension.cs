using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace MDIContainer.Control.Extensions
{   
   internal static class WindowBehaviorExtension
   {
      public static void Maximize(this MDIWindow window)
      {
         if (window.IsResizable)
         {            
            Canvas.SetTop(window, 0.0);
            Canvas.SetLeft(window, 0.0);              

            AnimateResize(window, window.Container.ActualWidth - 4, window.Container.ActualHeight  - 4, true);

            window.WindowState = WindowState.Maximized;       
         }
      }

      public static void Normalize(this MDIWindow window)
      {
         Canvas.SetTop(window, window.LastTop);
         Canvas.SetLeft(window, window.LastLeft);         

         AnimateResize(window, window.LastWidth, window.LastHeight, false);

         window.WindowState = WindowState.Normal;
      }

      public static void Minimize(this MDIWindow window)
      {         

         var index = window.Container.MinimizedWindowsCount;

         window.LastWidth = window.ActualWidth;
         window.LastHeight = window.ActualHeight;
         Canvas.SetTop(window, window.Container.ActualHeight - 32);
         Canvas.SetLeft(window, index * 205);

         RemoveWindowLock(window);
         AnimateResize(window, 200, 32, true);         

         window.WindowState = WindowState.Minimized;

         window.Tumblr.Source = window.CreateSnapshot();
      }

      private static void AnimateResize(MDIWindow window, double newWidth, double newHeight, bool lockWindow)
      {
         window.LayoutTransform = new ScaleTransform();
       
         var widthAnimation = new DoubleAnimation(window.ActualWidth, newWidth, new Duration(TimeSpan.FromMilliseconds(10)));         
         var heightAnimation = new DoubleAnimation(window.ActualHeight, newHeight, new Duration(TimeSpan.FromMilliseconds(10)));

         if (lockWindow == false)
         {
            widthAnimation.Completed += (s, e) => window.BeginAnimation(FrameworkElement.WidthProperty, null);
            heightAnimation.Completed += (s, e) => window.BeginAnimation(FrameworkElement.HeightProperty, null);
         }

         window.BeginAnimation(FrameworkElement.WidthProperty, widthAnimation, HandoffBehavior.Compose);
         window.BeginAnimation(FrameworkElement.HeightProperty, heightAnimation, HandoffBehavior.Compose);
      }

      public static void ToggleMaximize(this MDIWindow window)
      {         
         if (window.WindowState == WindowState.Maximized)
         {
            window.Normalize();
         }
         else
         {
            window.Maximize();
         }
      }      

      public static void ToggleMinimize(this MDIWindow window)
      {
         if (window.WindowState != WindowState.Minimized)
         {
            window.Minimize();
         }
         else
         {            
            switch (window.PreviousWindowState)
            {
               case WindowState.Maximized:
                  window.Maximize();
                  break;
               case WindowState.Normal:
                  window.Normalize();
                  break;
               default:
                  throw new NotSupportedException("Invalid WindowState");
            }
         }
      }

      public static void RemoveWindowLock(this MDIWindow window)
      {
         window.BeginAnimation(FrameworkElement.WidthProperty, null);
         window.BeginAnimation(FrameworkElement.HeightProperty, null);
      }
   }
}