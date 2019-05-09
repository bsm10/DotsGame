﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace DotsGame
{
    public class Links //: IEqualityComparer<Links>
    {
        public Dot Dot1;
        public Dot Dot2;

        //private float cost;
        public float Distance => (float)Math.Sqrt(Math.Pow(Math.Abs(Dot1.X - Dot2.X), 2) +
                                Math.Pow(Math.Abs(Dot1.Y - Dot2.Y), 2));
        public override string ToString()
        {
            string s = string.Empty;
            if (Dot1.Own == 1 & Dot2.Own == 1) s = " Player";
            if (Dot1.Own == 2 & Dot2.Own == 2) s = " Computer";
            if (Dot1.Own == 0 | Dot2.Own == 0) s = " None";

            return Dot1.X + ":" + Dot1.Y + "-" + Dot2.X + ":" + Dot2.Y + s + " Cost - " + Distance.ToString() + " Fixed " + Fixed.ToString();
        }
        public override int GetHashCode()
        {
            //Check whether the object is null
            if (ReferenceEquals(this, null)) return 0;

            //Get hash code for the Dot1
            int hashLinkDot1 = Dot1.GetHashCode();

            //Get hash code for the Dot2
            int hashLinkDot2 = Dot2.GetHashCode();

            //Calculate the hash code for the Links
            return hashLinkDot1 * hashLinkDot2;
        }

        public bool Blocked => (Dot1.Blocked & Dot2.Blocked);

        public bool Fixed => (Dot1.Fixed | Dot2.Fixed);


        public Links(Dot dot1, Dot dot2)
        {
            Dot1 = dot1;
            Dot2 = dot2;
            //if (dot1.BlokingDots.Count > dot2.BlokingDots.Count) dot2.BlokingDots.AddRange(dot1.BlokingDots);
            //if (dot2.BlokingDots.Count > dot1.BlokingDots.Count) dot1.BlokingDots.AddRange(dot2.BlokingDots);
        }

        public bool Equals(Links otherLink)//Проверяет равенство связей по точкам
        {
            return GetHashCode().Equals(otherLink.GetHashCode());
        }

    }
    class LinksComparer : IEqualityComparer<Links>
    {
        public bool Equals(Links link1, Links link2)
        {
            
            return link1.Equals(link2);
        }
        
        // If Equals() returns true for a pair of objects 
        // then GetHashCode() must return the same value for these objects.

        public int GetHashCode(Links links)
        {
            //Check whether the object is null
            if (ReferenceEquals(links, null)) return 0;

            //Get hash code for the Name field if it is not null.
            int hashLinkDot1 = links.Dot1.GetHashCode();

            //Get hash code for the Code field.
            int hashLinkDot2 = links.Dot2.GetHashCode();

            //Calculate the hash code for the product.
            return hashLinkDot1 * hashLinkDot2;
        }

    }
    public class ComparerDots : IComparer<Dot>
    {
        public int Compare(Dot d1, Dot d2)
        {
            if (d1.X.CompareTo(d2.X) != 0)
            {
                return d1.X.CompareTo(d2.X);
            }
            else if (d1.Y.CompareTo(d2.Y) != 0)
            {
                return d1.Y.CompareTo(d2.Y);
            }
            else
            {
                return 0;
            }
        }
    }
    public class ComparerDotsByOwn : IComparer<Dot>
    {
        public int Compare(Dot d1, Dot d2)
        {
            if (d1.X.CompareTo(d2.Own) != 0)
            {
                return d1.Own.CompareTo(d2.Own);
            }
            else if (d1.Own.CompareTo(d2.Own) != 0)
            {
                return d1.Own.CompareTo(d2.Own);
            }
            else
            {
                return 0;
            }
        }
    }
    public class Dot: IEquatable<Dot>
    {
        private bool _Blocked; 
        public bool Blocked
        {
            get => _Blocked;
            set
            {
                _Blocked = value;
                if (_Blocked)
                {
                    IndexRelation = 0;
                    if (NeiborDots.Count > 0)
                    {
                        foreach (Dot d in NeiborDots)
                        {
                            if (d.Blocked == false) d.IndexRelation = d.IndexDot;
                        }
                    }
                }
            }
        }
        /// <summary>
        /// Список точек, которые блокируются этой точкой
        /// </summary>
        public List<Dot> BlokingDots { get; }
        /// <summary>
        /// Точки по соседству с єтой точкой
        /// </summary>
        public List<Dot> NeiborDots { get; } = new List<Dot>();
        public bool Fixed { get; set; }
        public int CountBlockedDots { get=>BlokingDots.Count; }
        public bool Selected { get; set; }
        public int Own { get; set; }
        private int rating;
        public int Rating 
        {
            get
            {
                return rating;
            }
            set
            {
                rating = value;
                //foreach(Dot d in NeiborDots)
                //{
                //    //if(Math.Sqrt(Math.Pow(Math.Abs(d.x -x),2) + Math.Pow(Math.Abs(d.y -y),2))==1)
                //    //{
                //        if (rating < d.rating) d.Rating = rating;
                //        else rating = d.Rating;
                //    //}
                    
                //}
            }
        }
        public bool Marked { get; set; }
        public string Tag { get; set; } = string.Empty;

        private int _IndexDot;
        public int IndexDot
        {
            get
            {
                return _IndexDot;
            }
            set
            {
                _IndexDot = value;
                _IndexRel = _IndexDot;
            }
        }

        public bool BonusDot { get; set; }
        public Dot DotCopy
        {
            get
            {
                return (Dot)MemberwiseClone();
                //Dot d = new Dot(x,y,Own);
                //d.Blocked=Blocked;
                //return d;
            }
        }
        public int iNumberPattern { get; set; }

        public Dot(int x, int y, int Owner = 0, int NumberPattern = 0, int Rating = 0)
        {
            X = x;
            Y = y;
            BlokingDots = new List<Dot>();
            Own = Owner;
            iNumberPattern = NumberPattern;
            this.Rating = Rating;
            //IndexRelation = IndexDot;
        }

        public Dot(MouseEventArgs p)
        {
            X = p.X;
            Y = p.Y;
            BlokingDots = new List<Dot>();
            Own = 0;
            iNumberPattern = 0;
            Rating = Rating;
        }
        //public Dot(Point p)
        //{
        //    x = p.X;
        //    y = p.Y;
        //    BlokingDots = new List<Dot>();
        //    Own = 0;
        //    iNumberPattern = 0;
        //    Rating = Rating;
        //}

        /// <summary>
        /// Восстанавливаем первоначальное состояние точки
        /// </summary>
        public void Restore()
        {
            BlokingDots.Clear();
            Own = 0;
            iNumberPattern = 0;
            Rating = 0;
            Tag = "";
        }
        public void UnmarkDot()
        {
            Marked = false;
            PatternsFirstDot = false;
            PatternsMoveDot = false;
            PatternsAnyDot = false;
            PatternsEmptyDot = false;
        }
        /// <summary>
        /// Удаляем метки паттернов
        /// </summary>
        public void PatternsRemove()
        {
            PatternsFirstDot = false;
            PatternsMoveDot  = false;
            PatternsAnyDot   = false;
            PatternsEmptyDot = false;
        }
        public bool PatternsFirstDot {get; set;}
        public bool PatternsMoveDot { get; set; }
        public bool PatternsAnyDot { get; set; }
        public bool PatternsEmptyDot { get; set; }

        public override string ToString()
        {
            string s;
            if (Own == 1) s = " Player";
            else if (Own == 2) s = " Computer";
            else s = " None";
            s = Blocked ? X + ":" + Y + s + " Blocked" : X + ":" + Y + s + " Rating: " + Rating + "; " + Tag;
            return s;
        }

        public string DotStatistic
        {
            get
            {
                string dotstatistic = String.Empty;
                //GetType().GetProperties().ToDictionary(p => p.Name, p => p.GetValue(d)?.ToString());
                foreach (PropertyInfo p in GetType().GetProperties())
                {
                    dotstatistic = dotstatistic + "\r\n" + p.ToString();
                }
                return dotstatistic;
            }
        }

        public bool Equals(Dot dot)//Проверяет равенство точек по координатам - это для реализации  IEquatable<Dot>
        {
            return (X == dot.X) & (Y == dot.Y);
        }
        //public bool IsNeiborDots(Dot dot)//возвращает истину если соседние точки рядом. 
        //{
        //    if (dot.Blocked | dot.Blocked | dot.Own != Own)
        //    {
        //        return false;
        //    }
        //    return Math.Abs(x -dot.x) <= 1 & Math.Abs(y -dot.y) <= 1;

        //}
        private int _IndexRel;
        public int IndexRelation
        {
            get { return _IndexRel; }
            set
            {
                _IndexRel = value;
                if (NeiborDots.Count > 0)
                {
                    foreach (Dot d in NeiborDots)
                    {
                        if (d.Blocked == false)
                        {
                            if (d.IndexRelation != _IndexRel & _IndexRel != 0)
                            {
                                d.IndexRelation = _IndexRel;
                            }
                        }
                    }

                }
            }

        }

        public static explicit operator Dot(Point v)
        {
            return new Dot(v.X,v.Y);
        }

        public int X { get; set; }
        public int Y { get; set; }


        //public bool ValidMove
        //{
        //    get { return Blocked == false && Own == 0; }
        //}
    }
}
