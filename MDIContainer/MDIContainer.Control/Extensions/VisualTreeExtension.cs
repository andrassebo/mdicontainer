using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace MDIContainer.Control.Extensions
{
   internal static class VisualTreeExtension
   {
      public static TParent FindSpecificParent<TParent>(FrameworkElement element)
         where TParent : FrameworkElement
      {
         var current = VisualTreeHelper.GetParent(element) as FrameworkElement;

         while (current != null)
         {
            if (current is TParent parent)
               return parent;

            current = VisualTreeHelper.GetParent(current) as FrameworkElement;
         }

         return null!;
      }

      public static MDIWindow FindMDIWindow(FrameworkElement sender)
      {
         return FindSpecificParent<MDIWindow>(sender)!;
      }
   }
}
