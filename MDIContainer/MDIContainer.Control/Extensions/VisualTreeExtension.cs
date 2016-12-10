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
      public static TParent FindSpecificParent<TParent>(FrameworkElement sender)
         where TParent : FrameworkElement
      {
         var current = sender;
         var p = VisualTreeHelper.GetParent(current) as FrameworkElement;

         if (p != null && p.GetType() != typeof(TParent))
         {
            p = FindSpecificParent<TParent>(p);
         }

         return p as TParent;
      }

      public static MDIWindow FindMDIWindow(FrameworkElement sender)
      {
         return FindSpecificParent<MDIWindow>(sender);
      }
   }
}
