﻿using System;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Microsoft.Phone.Controls;

namespace Ocell.Transitions
{
    public class OpacityTransition : ITransition
    {

        readonly Storyboard _storyboard;

        public OpacityTransition(Storyboard sb)
        {
            _storyboard = sb;
        }
        

        #region ITransition Members



        public void Begin()
        {
            _storyboard.Begin();
        }



        public event EventHandler Completed
        {
            add
            {
                _storyboard.Completed += value;
            }
            remove
            {
                _storyboard.Completed -= value;
            }
        }

        public ClockState GetCurrentState()
        {
            return _storyboard.GetCurrentState();
        }

        public TimeSpan GetCurrentTime()
        {
            throw new NotImplementedException();
        }

        public void Pause()
        {
            _storyboard.Pause();
        }

        public void Resume()
        {
            _storyboard.Resume();
        }

        public void Seek(TimeSpan offset)
        {

        }

        public void SeekAlignedToLastTick(TimeSpan offset)
        {

        }

        public void SkipToFill()
        {
            _storyboard.SkipToFill();
        }



        public void Stop()
        {
            _storyboard.Stop();
        }
        #endregion
    }

    public class OpacityTransitionElement : TransitionElement
    {
        public override ITransition GetTransition(UIElement element)
        {
            Storyboard myStoryboard = CreateStoryboard(0.0, 1.0);
            Storyboard.SetTarget(myStoryboard, element);
            return new OpacityTransition(myStoryboard);
        }

        private Storyboard CreateStoryboard(double from, double to)
        {
            Storyboard result = new Storyboard();
            DoubleAnimation animation = new DoubleAnimation();
            animation.From = from;
            animation.To = to;
            animation.Duration = new Duration(TimeSpan.FromMilliseconds(250));
            Storyboard.SetTargetProperty(animation, new PropertyPath(UIElement.OpacityProperty));
            result.Children.Add(animation);
            return result;
        }
    }
}
