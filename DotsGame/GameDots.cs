﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DotsGame
{
    public static class DebugInfo
    {
        public static string textDBG = string.Empty;
        public static string textDBG1 = string.Empty; 
        public static List<Dot> lstDBG1 = new List<Dot>();
        public static List<string> lstDBG2 = new List<string>();
        
    }

    public class GameDots : IEnumerator, IEnumerable, IGame
    {
        private const int PLAYER_DRAW = -1;
        private const int PLAYER_NONE = 0;
        private const int PLAYER_HUMAN = 1;
        private const int PLAYER_COMPUTER = 2;

        private IList<Dot> list_moves; //список ходов

        public IList<Dot> ListMoves // список ходов
        {
            get { return list_moves; }
            set { list_moves = value; }
        }

        private IList<Dot> stackMoves; //список ходов

        public Dot LastMove
        {
            get
            {
                if (ListMoves.Count == 0)//когда выбирается первая точка для хода
                {
                    var random = new Random(DateTime.Now.Millisecond);
                    var q = from Dot d in Dots
                            where d.x <= BoardWidth / 2 & d.x > BoardWidth / 3
                                                    & d.y <= BoardHeight / 2 & d.y > BoardHeight / 3
                            orderby (random.Next())
                            select d;
                    return q.First();

                }
                else
                {
                    return ListMoves.Last();
                }
            }
        }
        public List<Dot> ListDotsForDrawing
        {// главная коллекция для отрисовки партии
            get
            {
                return ListMoves.ToList();
            }
        }
        private Dot best_move; //ход который должен сделать комп
        private List<Dot> dots_in_region;//записывает сюда точки, которые окружают точку противника
        private IList<Dot> _Dots; // главная коллекция
        public IList<Dot> Dots
        {
            get
            {
                return _Dots;
            }
            set
            {
                _Dots = value;
            }
        }
        /// <summary>
        /// Возвращает список не занятых точек
        /// </summary>
        private List<Dot> Board_ValidMoves
        {
            get
            {
                return (from Dot d in _Dots where d.ValidMove select d).ToList();
            }
        }
        private List<Dot> Board_NotEmptyNonBlockedDots
        {
            get
            {
                return (from Dot d in _Dots where d.Own != 0 && d.Blocked == false select d).ToList();
            }
        }
        private List<Dot> Board_AllNotBlockedDots
        {
            get
            {
                return _Dots.Where(d=>!d.Blocked).ToList();
            }
        }
        private GameDots CopyDots
        {
            get
            {
                GameDots ad = new GameDots(BoardWidth,BoardHeight);
                for (int i = 0; i < Dots.Count; i++)
                {
                    ad._Dots[i].Blocked = Dots[i].Blocked;
                    ad._Dots[i].Fixed = Dots[i].Fixed;
                    ad._Dots[i].IndexDot = Dots[i].IndexDot;
                    ad._Dots[i].Own = Dots[i].Own;
                    ad._Dots[i].x = Dots[i].x;
                    ad._Dots[i].y = Dots[i].y;
                }
                return ad;
            }
        }


#if DEBUG
        Stopwatch stopWatch = new Stopwatch();//для диагностики времени выполнения
        Stopwatch sW_BM = new Stopwatch();
        Stopwatch sW2 = new Stopwatch();
#endif

        private int win_player;//переменная получает номер игрока, котрый окружил точки
        int position = -1;
        private int nBoardWidth;//размер поля
        private int nBoardHeight;//размер поля
        public int BoardWidth
        {
            get { return nBoardWidth; }
            set { nBoardWidth = value; }
        }
        public int BoardHeight
        {
            get { return nBoardHeight; }
            set { nBoardHeight = value; }
        }
        private int BoardSize
        {
            get
            {
                return nBoardWidth * nBoardHeight;
            }
        }
        public GameDots(int boardwidth, int boardheight)
        {
            int counter = 0;
            int ind;
            BoardHeight = boardheight;
            BoardWidth = boardwidth;
            _Dots = new List<Dot>(boardwidth * boardheight); // главная коллекция точек
            for (int i = 0; i < boardwidth; i++)
            {
                for (int j = 0; j < boardheight; j++)
                {
                    ind = IndexDot(i, j);
                    _Dots.Add(new Dot(i, j));
                    _Dots[ind].IndexDot = ind;
                    if (i == 0 | i == (boardwidth - 1) | j == 0 | j == (boardheight - 1)) _Dots[ind].Fixed = true;
                    counter += 1;
                }
            }

            lnks = new List<Links>();
            list_moves = new List<Dot>();
            stackMoves = new List<Dot>();
            dots_in_region = new List<Dot>();

        }
        public class DotEq : EqualityComparer<Dot>
        {
            public override int GetHashCode(Dot dot)
            {
                int hCode = dot.x ^ dot.y;
                return hCode.GetHashCode();
            }

            public override bool Equals(Dot d1, Dot d2)
            {
                return (d1.x == d2.x & d1.y == d2.y & d1.Rating == d2.Rating); 
            }
        }
        public int Count
        {
            get
            {
                return _Dots.Count;
            }
        }
        public Dot this[int i, int j]//Индексатор возвращает элемент из массива по его индексу
        {
            get
            {
                if (i >= BoardWidth) i = BoardWidth - 1;
                if (j >= BoardHeight) j = BoardHeight - 1;
                if (i < 0) i = 0;
                if (j < 0) j = 0;
                return _Dots[IndexDot(i, j)];
            }
        }
        private void Add(Dot dot)//добавляет точку в массив
        {
            int ind = IndexDot(dot.x, dot.y);
            if (DotIndexCheck(dot.x, dot.y))
            {
                _Dots[ind].Own = dot.Own;
                if (dot.Own != 0) _Dots[ind].IndexRelation = _Dots[ind].IndexDot;
                _Dots[ind].Blocked = false;
                if (dot.x == 0 | dot.x == (BoardWidth - 1) | dot.y == 0 | 
                    dot.y == (BoardHeight - 1)) _Dots[ind].Fixed = true;
                AddNeibor(_Dots[ind]);
                stackMoves.Add(_Dots[ind]);
            }
        }
        private void AddNeibor(Dot dot)
        {
            //выбрать соседние точки, если такие есть
            var q = from Dot d in _Dots where d.Own == dot.Own & Distance(dot, d) < 2 select d;

            foreach (Dot d in q)
            {
                if (d != dot)
                {
                    if (dot.Rating > d.Rating) dot.Rating = d.Rating;
                    if (dot.NeiborDots.Contains(d) == false) dot.NeiborDots.Add(d);
                    if (d.NeiborDots.Contains(dot) == false) d.NeiborDots.Add(dot);
                }
            }
            MakeIndexRelation(dot);
        }
        private void RemoveNeibor(Dot dot)
        {
            foreach (Dot d in Dots)
            {
                if (d.NeiborDots.Contains(dot)) d.NeiborDots.Remove(dot);
            }
        }
        private void Remove(Dot dot)//удаляет точку из массива
        {
            int ind = IndexDot(dot.x, dot.y);
            if (DotIndexCheck(dot.x, dot.y))
            {
                int i = _Dots[ind].IndexDot;
                RemoveNeibor(dot);
                _Dots[ind] = new Dot(dot.x, dot.y);
                _Dots[ind].IndexDot = i;
                _Dots[ind].IndexRelation = i;
                ListMoves.Remove(dot);
                stackMoves.Remove(dot);
            }
        }
        private float Distance(Dot dot1, Dot dot2)//расстояние между точками
        {
            return (float)Math.Round(Math.Sqrt(Math.Pow((dot1.x -dot2.x), 2) + Math.Pow((dot1.y -dot2.y), 2)),1);
        }
        /// <summary>
        /// возвращает список соседних точек заданной точки
        /// </summary>
        /// <param name="dot"> точка Dot из массива точек типа ArrayDots </param>
        /// <returns> список точек </returns>
        private List<Dot> NeiborDotsSNWE(Dot dot)//SNWE -S -South, N -North, W -West, E -East
        {
            Dot[] dts = new Dot[4] {
                                    this[dot.x + 1, dot.y],
                                    this[dot.x - 1, dot.y],
                                    this[dot.x, dot.y + 1],
                                    this[dot.x, dot.y - 1]
                                    };
            return dts.ToList();
        }
        private List<Dot> NeiborDots(Dot dot)
        {
            Dot[] dts = new Dot[8] {
                                    this[dot.x + 1, dot.y],
                                    this[dot.x - 1, dot.y],
                                    this[dot.x, dot.y + 1],
                                    this[dot.x, dot.y - 1],
                                    this[dot.x + 1, dot.y + 1],
                                    this[dot.x - 1, dot.y - 1],
                                    this[dot.x - 1, dot.y + 1],
                                    this[dot.x + 1, dot.y - 1]
                                    };
            return dts.ToList();
        }
        public void UnmarkAllDots()
        {
            foreach (Dot d in _Dots) d.UnmarkDot();
        }
        private int MinX()
        {
            var q = from Dot d in _Dots where d.Own != 0 & d.Blocked == false select d;
            int minX = BoardWidth;
            foreach (Dot d in q)
            {
                if (minX > d.x) minX = d.x;
            }
            return minX;
        }
        private int MaxX()
        {
            var q = from Dot d in _Dots where d.Own != 0 & d.Blocked == false select d;
            int maxX = 0;
            foreach (Dot d in q)
            {
                if (maxX < d.x) maxX = d.x;
            }
            return maxX;
        }
        private int MaxY()
        {
            var q = from Dot d in _Dots where d.Own != 0 & d.Blocked == false select d;
            int maxY = 0;
            foreach (Dot d in q)
            {
                if (maxY < d.y) maxY = d.y;
            }
            return maxY;
        }
        private int MinY()
        {
            var q = from Dot d in _Dots where d.Own != 0 & d.Blocked == false select d;
            int minY = BoardHeight;
            foreach (Dot d in q)
            {
                if (minY > d.y) minY = d.y;
            }
            return minY;
        }
        private int CountNeibourDots(int Owner)//количество точек определенного цвета возле пустой точки
        {
            var q = from Dot d in _Dots
                    where d.Blocked == false & d.Own == 0 &
                    _Dots[IndexDot(d.x + 1, d.y -1)].Blocked == false & _Dots[IndexDot(d.x + 1, d.y -1)].Own == Owner & 
                    _Dots[IndexDot(d.x + 1, d.y + 1)].Blocked == false & _Dots[IndexDot(d.x + 1, d.y + 1)].Own == Owner
                    | d.Own == 0 & _Dots[IndexDot(d.x, d.y -1)].Blocked == false & _Dots[IndexDot(d.x, d.y -1)].Own == Owner & _Dots[IndexDot(d.x, d.y + 1)].Blocked == false & _Dots[IndexDot(d.x, d.y + 1)].Own == Owner
                    | d.Own == 0 & _Dots[IndexDot(d.x -1, d.y -1)].Blocked == false & _Dots[IndexDot(d.x -1, d.y -1)].Own == Owner & _Dots[IndexDot(d.x -1, d.y + 1)].Blocked == false & _Dots[IndexDot(d.x -1, d.y + 1)].Own == Owner
                    | d.Own == 0 & _Dots[IndexDot(d.x -1, d.y -1)].Blocked == false & _Dots[IndexDot(d.x -1, d.y -1)].Own == Owner & _Dots[IndexDot(d.x + 1, d.y + 1)].Blocked == false & _Dots[IndexDot(d.x + 1, d.y + 1)].Own == Owner
                    | d.Own == 0 & _Dots[IndexDot(d.x -1, d.y + 1)].Blocked == false & _Dots[IndexDot(d.x -1, d.y + 1)].Own == Owner & _Dots[IndexDot(d.x + 1, d.y -1)].Blocked == false & _Dots[IndexDot(d.x + 1, d.y -1)].Own == Owner
                    select d;
            return q.Count();
        }
        /// <summary>
        /// Вычисляет индекс точки в списке по ее координатам
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        private int IndexDot(int x, int y)
        {

            int index = x * BoardHeight + y;

            return index;
        }
        /// <summary>
        /// Проверка, находится ли точка на игровой доске
        /// </summary>
        /// <returns></returns>
        private bool DotIndexCheck(int x, int y)
        {
            return (x >= 0 && x < BoardWidth &&
                    y >= 0 && y < BoardHeight);
        }
        /// <summary>
        /// Проверка, находится ли точка на игровой доске
        /// </summary>
        /// <returns></returns>
        private bool DotIndexCheck(Dot dot)
        {
            return dot==null? false : (dot.x >= 0 && dot.x < BoardWidth &&
                    dot.y >= 0 && dot.y < BoardHeight);
        }
        private List<Dot> EmptyNeibourDots(int Owner)//список не занятых точек возле определенной точки
        {
            List<Dot> ld = new List<Dot>();
            foreach (Dot d in _Dots)
            {
                if (d.Own == Owner)
                {
                    var q = from Dot dot in _Dots
                            where dot.Blocked == false & dot.Own == 0 & Distance(dot, d) < 2
                            select dot;
                    foreach (Dot empty_d in q)
                    {
                        if (ld.Contains(empty_d) == false) ld.Add(empty_d);
                    }
                }
            }
            return ld;
        }
        private int MakeIndexRelation(Dot dot)
        {
            if (dot.NeiborDots.Count > 0)
            {
                foreach (Dot d in dot.NeiborDots)
                {
                    if (dot.Blocked == false & dot.Own == d.Own) d.IndexRelation = dot.IndexRelation;
                }
            }
            else
            {
            }
            return dot.IndexRelation;
        }
        public void Clear()//Не очищает список, а заменяет точки на Own == 0
        {
            foreach (Dot d in _Dots)
            {
                d.Own = 0;
                d.Marked = false;
                d.Blocked = false;
                d.BlokingDots.Clear();
                d.Rating = 0;
            }
            ListLinks.Clear();
            ListMoves.Clear();

        }
        /// <summary>
        /// Пересканирует блокированные точки и устанавливает статус Blocked
        /// </summary>
        private void RescanBlockedDots()
        {
            //-------------------Rescan Blocked Dots------------------
            List<Dot> blockedDots = new List<Dot>();
            foreach (Dot d in (from Dot d in Dots
            where d.BlokingDots.Count > 0
            select d))
            {
                blockedDots.AddRange(d.BlokingDots);
            }
            foreach (Dot d in blockedDots) d.Blocked = true;


            ////-------------------Rescan Blocked EmptyDots---------------------

            //while ((from Dot d in Dots
            //        where d.Own == 0 && d.Blocked == false &&
            //              d.x + 1 < BoardWidth && d.x - 1 > -1 &&
            //              d.y + 1 < BoardHeight && d.y - 1 > -1 &&
            //              this[d.x + 1, d.y].Blocked |
            //              this[d.x - 1, d.y].Blocked |
            //              this[d.x, d.y + 1].Blocked |
            //              this[d.x, d.y - 1].Blocked
            //        select d.Blocked = true).Count() != 0) ;
        }

        /// <summary>
        /// проверяет заблокирована ли точка. Перед использованием функции надо установить flg_own
        /// </summary>
        /// <param name="dot">поверяемая точка</param>
        /// <param name="flg_own">владелец проверяемой точки, этот параметр нужен для рекурсии</param>
        /// <returns></returns>
        private bool DotIsFree(Dot dot, int flg_own) 
        {
            dot.Marked = true;
            if (dot.x == 0 | dot.y == 0 | dot.x == BoardWidth - 1 | dot.y == BoardHeight - 1)
            {
                return true;
            }
            Dot[] d = new Dot[4] { this[dot.x + 1, dot.y], this[dot.x - 1, dot.y], this[dot.x, dot.y + 1], this[dot.x, dot.y - 1] };
            //--------------------------------------------------------------------------------
            if (flg_own == 0)// если точка не принадлежит никому и рядом есть незаблокированные точки -эта точка считается свободной(незаблокированной)
            {
                var q = from Dot fd in d where fd.Blocked == false select fd;
                if (q.Count() > 0)
                {
                    return true;
                }
                else return false;

            }
            //----------------------------------------------------------------------------------
            for (int i = 0; i < 4; i++)
            {
                if (d[i].Marked == false)
                {
                    if (d[i].Own == 0 | d[i].Own == flg_own | d[i].Own != flg_own 
                      & d[i].Blocked & d[i].BlokingDots.Contains(dot) == false)
                    {
                        if (DotIsFree(d[i], flg_own))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }
        private void RebuildDots()
        {
            GameDots _Dots = new GameDots(BoardWidth,BoardHeight);
            foreach (Dot dot in stackMoves)
            {
                if(ListMoves.Contains(dot))_Dots.MakeMove(dot, dot.Own, addForDraw: true);
                else _Dots.MakeMove(dot, dot.Own);
            }
            _Dots.RescanBlockedDots();
            Dots = _Dots.Dots;
            ListMoves = _Dots.ListMoves;

            LinkDots();
        }
        public void UndoMove(Dot dot)//поле отмена хода
        {
            Remove(dot);
            RebuildDots();
        }
        private List<Dot> CheckPattern(int Owner)
        {
            List<Pattern> found_patterns = CheckPatternInPatterns(Owner);
            List<Dot> found_moves = new List<Dot>();
            //if (p != null)
            foreach (Pattern p in found_patterns)
            {
                Dot move_dot = p.ResultDot;
                move_dot.iNumberPattern = p.PatternNumber;
                found_moves.Add(move_dot);
                //return move_dot;
            }
            return found_moves; //null;

        }

        /// <summary>
        /// Проверяет паттерн, если есть совпадение, возвращает его номер
        /// </summary>
        /// <param name="pt"></param>
        /// <param name="Owner"></param>
        /// <returns></returns>
        private List<Pattern> CheckPatternInPatterns(int Owner)
        {
            List<Pattern> ptrns = new List<Pattern>();
            //var pat = from Dot d in Board_NotEmptyNonBlockedDots//Dots
            //          where d.Blocked == false
            //          select d;

            //foreach (Dot d in pat)
            //{
            //    foreach (Pattern p in Patterns)
            //    {
            //        bool flag = true;
            //        if (!this[d.x + p.dXdY_ResultDot.dX,
            //                  d.y + p.dXdY_ResultDot.dY].ValidMove) continue; //если результирующая точка недопустимый ход, пропускаем паттерн
            //        foreach (DotInPattern dt in p.DotsPattern)
            //        {
            //            int enemy_own = Owner == 1 ? 2 : 1;
            //            //int DotOwner = 0;
            //            switch (dt.Owner)
            //            {
            //                case "enemy":
            //                    if (DotIndexCheck(d.x + dt.dX, d.y + dt.dY) == false ||
            //                    this[d.x + dt.dX, d.y + dt.dY].Own != enemy_own) flag = false;
            //                    break;
            //                case "0":
            //                    if (DotIndexCheck(d.x + dt.dX, d.y + dt.dY) == false ||
            //                    this[d.x + dt.dX, d.y + dt.dY].Own != 0) flag = false;
            //                    break;
            //                case "owner":
            //                    if (DotIndexCheck(d.x + dt.dX, d.y + dt.dY) == false ||
            //                    this[d.x + dt.dX, d.y + dt.dY].Own != Owner) flag = false;
            //                    break;
            //                case "!=enemy":
            //                    if (DotIndexCheck(d.x + dt.dX, d.y + dt.dY) == false ||
            //                    this[d.x + dt.dX, d.y + dt.dY].Own == enemy_own) flag = false;
            //                    break;
            //            }
            //            if (flag == false) break;
            //        }
            //        if (flag)
            //        {
            //            p.FirstDot = d;
            //            ptrns.Add(p);
            //        }
            //    }
            //}

            return ptrns;//null;
        }
        private int count_in_region;
        private int count_blocked_dots;

        /// <summary>
        /// Функция делает ход игрока 
        /// </summary>
        /// <param name="dot">точка куда делается ход</param>
        /// <param name="Owner">владелец точки -целое 1-Игрок или 2 -Компьютер</param>
        /// <returns>количество окруженных точек или -1 если недопустимый ход</returns>
        public int MakeMove(Dot dot, int Owner = 0, bool addForDraw = false)//
        {

            if (this[dot.x, dot.y].ValidMove)
            {
                if (Owner != 0) dot.Own = Owner;
                Add(dot); //если точка не занята
            }
            else return -1;//в случае невозможного хода
            //--------------------------------
            int res = CheckBlocked(dot.Own);
            //--------------------------------
            count_blocked_dots = (from Dot d in Dots where d.Blocked select d).Count();
            if (addForDraw)
            {
                ListMoves.Add(_Dots[IndexDot(dot.x, dot.y)]);
                LinkDots();//перестроить связи точек
                _ListLinksForDrawing = lnks.ToList();
            }
            return res;
        }


        /// <summary>
        /// проверяет блокировку точек, маркирует точки которые блокируют, возвращает количество окруженных точек
        /// </summary>
        /// <param name="arrDots"></param>
        /// <param name="last_moveOwner"></param>
        /// <returns>количество окруженных точек</returns>
        private int CheckBlocked(int last_moveOwner = 0)
        {
            int counter = 0;
            var checkdots = from Dot dots in this
                            where dots.Own != 0 | dots.Own == 0 & dots.Blocked
                            orderby dots.Own == last_moveOwner
                            select dots;
            lst_blocked_dots.Clear(); lst_in_region_dots.Clear();
            foreach (Dot d in checkdots)
            {
                UnmarkAllDots();
                if (DotIsFree(d, d.Own) == false)
                {
                    if (d.Own != 0) d.Blocked = true;
                    d.IndexRelation = 0;
                    var q1 = from Dot dots in this where dots.BlokingDots.Contains(d) select dots;
                    if (q1.Count() == 0)
                    {
                        UnmarkAllDots();
                        MarkDotsInRegion(d, d.Own);

                        foreach (Dot dr in lst_in_region_dots)
                        {
                            win_player = dr.Own;
                            count_in_region++;
                            foreach (Dot bd in lst_blocked_dots)
                            {
                                if (bd.Own != 0) counter += 1;
                                if (dr.BlokingDots.Contains(bd) == false & bd.Own != 0 & dr.Own != bd.Own)
                                {
                                    dr.BlokingDots.Add(bd);
                                }
                            }
                        }
                    }
                }
                else
                {
                    d.Blocked = false;
                }
            }
            RescanBlockedDots();
            if (lst_blocked_dots.Count == 0) win_player = 0;
            return lst_blocked_dots.Count;
        }

        /// <summary>
        /// Возвращает количество блокированных точек
        /// </summary>
        /// <param name="Owner">владелец блокированных точек</param>
        /// <returns></returns>
        private int CheckBlocked2(int Owner)
        {
            var checkdots = from Dot d in this
                            where d.Own == Owner && DotIsFree(d, Owner) == false 
                            select d;
            if (checkdots.Count() > 0) foreach (Dot d in checkdots) d.Blocked = true;

            var blocking_dots = from Dot d1 in this
                                where d1.Blocked
                                from Dot d2 in this
                                where d2.Blocked == false & Distance(d1,d2) == 1
                                select d2;

            
            lst_blocked_dots = checkdots.ToList();


            return lst_blocked_dots.Count;
        }


        private List<Dot> lst_blocked_dots = new List<Dot>();//список блокированных точек
        private List<Dot> lst_in_region_dots = new List<Dot>();//список блокирующих точек
        /// <summary>
        /// Определяет блокирующие точки и устанавливает этим точкам поле InRegion=true 
        /// </summary>
        /// <param name="blocked_dot">точка, которая блокируется</param>
        /// <param name="flg_own">владелец точки</param>
        private void MarkDotsInRegion(Dot blocked_dot, int flg_own)
        {
            blocked_dot.Marked = true;
            //Dot[] dts = new Dot[4] {
            //                        this[blocked_dot.x + 1, blocked_dot.y],
            //                        this[blocked_dot.x -1, blocked_dot.y],
            //                        this[blocked_dot.x, blocked_dot.y + 1],
            //                        this[blocked_dot.x, blocked_dot.y -1],
            //                        };
            
            //добавим точки которые попали в окружение
            if (lst_blocked_dots.Contains(blocked_dot) == false)
            {
                lst_blocked_dots.Add(blocked_dot);
            }
            //foreach (Dot _d in dts)
            foreach (Dot _d in NeiborDotsSNWE(blocked_dot))
            {
                if (_d.Own != 0 & _d.Blocked == false & _d.Own != flg_own)//_d-точка которая окружает
                {
                    //добавим в коллекцию точки которые окружают
                    if (lst_in_region_dots.Contains(_d) == false) lst_in_region_dots.Add(_d);
                }
                else
                {
                    if (_d.Marked == false & _d.Fixed == false)
                    {
                        _d.Blocked = true;
                        MarkDotsInRegion(_d, flg_own);
                    }
                }
            }
        }
        //+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        private void MakeRating()//возвращает массив вражеских точек вокруг заданной точки
        {
            int res;
            var qd = from Dot dt in this where dt.Own != 0 & dt.Blocked == false select dt;
            foreach (Dot dot in qd)
            {
                //if (dot.x > 0 & dot.y > 0 & dot.x < iMapSize -1 & dot.y < iMapSize -1)
                if (dot.x > 0 & dot.y > 0 & dot.x < BoardWidth - 1 & dot.y < BoardHeight - 1)
                {
                    Dot[] dts = new Dot[4] {this[dot.x + 1, dot.y], 
                                            this[dot.x -1, dot.y],
                                            this[dot.x, dot.y + 1],
                                            this[dot.x, dot.y -1]};
                    res = 0;
                    foreach (Dot item in dts)
                    {
                        if (item.Own != 0 & item.Own != dot.Own) res++;
                        else if (item.Own == dot.Own & item.Rating == 0)
                        {
                            res = -1;
                            break;
                        }
                    }
                    dot.Rating = res + 1;//точка без связей получает рейт 1
                }
            }
        }
        //+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        private List<Links> lnks;
        public List<Links> ListLinks 
        { 
            get {return lnks;}
        }
        private List<Links> _ListLinksForDrawing = new List<Links>();
        public List<Links> ListLinksForDrawing
        {
            get { return _ListLinksForDrawing; }
        }
        /// <summary>
        /// устанавливает связь между двумя точками и возвращает массив связей 
        /// </summary>
        private void LinkDots()
        {
            lnks.Clear();
            var qry = from Dot d1 in this
                      where d1.BlokingDots.Count > 0
                      from Dot d2 in this
                          //where d2.Own == d1.Own && d1.Blocked == d2.Blocked && d2.BlokingDots.Count > 0
                      where d2.Own == d1.Own && d1.Blocked == d2.Blocked
                      & Distance(d1, d2) < 2 & Distance(d1, d2) > 0 && d2.BlokingDots.Count > 0
                      || d2.Own == d1.Own && d1.Blocked == d2.Blocked && Distance(d1, d2) == 1 
                      //|| !d1.Blocked & d2.Blocked & Distance(d1, d2) == 1

                      select new Links(d1, d2);

            var temp = qry.Distinct(new LinksComparer());
            lnks = temp.ToList(); //обновляем основной массив связей - lnks              

            qry = from Links l1 in lnks
                  from Links l2 in lnks
                  where l1.Dot1.Equals(l2.Dot1) && Distance(l1.Dot2, l2.Dot2) < 2
                  select new Links(l1.Dot2, l2.Dot2);
            lnks.AddRange(qry.ToList());


        }
        /// <summary>
        /// функция проверяет не делается ли ход в точку, которая на следующем ходу будет окружена
        /// </summary>
        /// <param name="dot"></param>
        /// <param name="arrDots"></param>
        /// <param name="Player"></param>
        /// <returns></returns>
        private bool CheckDot(Dot dot, int Player)
        {
            int res = MakeMove(dot, Player);
            int pl = Player == 2 ? 1 : 2;
            if (win_player == pl)// первое условие -ход в уже оеруженный регион, 
            {
                UndoMove(dot);
                return true; // да будет окружена
            }
            //будет ли окружена на следующем ходу
            Dot dotEnemy = CheckMove(pl);
            if (dotEnemy != null)
            {
                res = MakeMove(dotEnemy, pl);
                UndoMove(dotEnemy);
                UndoMove(dot);
                return true; // да будет окружена
            }
            //нет не будет
            UndoMove(dot);
            return false;
        }
        //===============================================================================================================

        private List<Dot> RebuildLinks()
        {
            //List<Dot> lstDots = new List<Dot>();

            //var q = from Dot dot in this
            //        where dot.IndexDot == index & dot.NeiborDots.Count == 1
            //        select dot;

            return null;
        }
        //==============================================================================================
        /// <summary>
        /// проверяет ход в результате которого окружение.
        /// </summary>
        /// <param name="Owner">владелец проверянмых точек</param>
        /// <returns>Возвращает ход(точку) который завершает окружение</returns>
        private Dot CheckMove1(int Owner)
        {
            List<Dot> happy_dots = new List<Dot>();
            var qry = Board_ValidMoves.Where(
#region Query patterns Check

                         d =>
                         //     d.x + 1 < BoardWidth && d.x - 1 > -1 &&
                         //     d.y + 1 < BoardHeight && d.y - 1 > -1 &&
                                //       + 
                                //    d  
                                //       +
                          this[d.x + 1, d.y - 1].Blocked == false & this[d.x + 1, d.y + 1].Blocked == false &
                          this[d.x + 1, d.y - 1].Own == Owner & this[d.x + 1, d.y + 1].Own == Owner &
                          this[d.x + 1, d.y].Own != Owner
                                //+        
                                //   d     
                                //+       
                          | this[d.x - 1, d.y - 1].Blocked == false & this[d.x - 1, d.y + 1].Blocked == false &
                          this[d.x - 1, d.y - 1].Own == Owner & this[d.x - 1, d.y + 1].Own == Owner &
                          this[d.x - 1, d.y].Own != Owner
                                //      
                                //   d          
                                //+     +
                        | this[d.x + 1, d.y + 1].Blocked == false & this[d.x - 1, d.y + 1].Blocked == false &
                        this[d.x + 1, d.y + 1].Own == Owner & this[d.x - 1, d.y + 1].Own == Owner &
                        this[d.x, d.y + 1].Own != Owner
                                //+     + 
                                //   d          
                                //     
                        | this[d.x - 1, d.y - 1].Blocked == false & this[d.x + 1, d.y - 1].Blocked == false &
                         this[d.x - 1, d.y - 1].Own == Owner & this[d.x + 1, d.y - 1].Own == Owner &
                         this[d.x, d.y - 1].Own != Owner

                                //    +    
                                //    d   
                                //    +   
                          | this[d.x, d.y + 1].Blocked == false & this[d.x, d.y - 1].Blocked == false &
                              this[d.x, d.y - 1].Own == Owner & this[d.x, d.y + 1].Own == Owner 
                              //&
                              //this[d.x + 1, d.y].Own != Owner |
                              //this[d.x - 1, d.y].Own != Owner
                                //        
                                //+   d   +  
                                //       
                          | this[d.x - 1, d.y].Blocked == false & this[d.x + 1, d.y].Blocked == false &
                            this[d.x - 1, d.y].Own == Owner & this[d.x + 1, d.y].Own == Owner 
                            //&
                            //this[d.x, d.y + 1].Own != Owner &
                            //this[d.x, d.y - 1].Own != Owner

                                //+        
                                //   d     
                                //      +   
                          | this[d.x - 1, d.y - 1].Blocked == false & this[d.x + 1, d.y + 1].Blocked == false &
                          this[d.x - 1, d.y - 1].Own == Owner & this[d.x + 1, d.y + 1].Own == Owner 
                          //&
                          //this[d.x, d.y - 1].Own != Owner &
                          //this[d.x, d.y + 1].Own != Owner &
                          //this[d.x + 1, d.y].Own != Owner &
                          //this[d.x - 1, d.y].Own != Owner
                                //      +  
                                //   d     
                                //+        
                          | this[d.x - 1, d.y + 1].Blocked == false & this[d.x + 1, d.y - 1].Blocked == false &
                          this[d.x - 1, d.y + 1].Own == Owner & this[d.x + 1, d.y - 1].Own == Owner
                          //&
                          //this[d.x, d.y - 1].Own != Owner &
                          //this[d.x, d.y + 1].Own != Owner &
                          //this[d.x + 1, d.y].Own != Owner &
                          //this[d.x - 1, d.y].Own != Owner

                                //      +
                                //+  d
                        | this[d.x - 1, d.y].Blocked == false & this[d.x + 1, d.y - 1].Blocked == false &
                          this[d.x - 1, d.y].Own == Owner & this[d.x + 1, d.y - 1].Own == Owner
                          &
                         this[d.x, d.y - 1].Own != Owner 

                         //& this[d.x + 1, d.y].Own != Owner &
                         //this[d.x - 1, d.y - 1].Own != Owner &
                         //this[d.x + 1, d.y + 1].Own != Owner &
                         // this[d.x, d.y + 1].Own != Owner

                        // +  d
                        //       +
                        | this[d.x - 1, d.y].Blocked == false & this[d.x + 1, d.y + 1].Blocked == false &
                          this[d.x - 1, d.y].Own == Owner & this[d.x + 1, d.y + 1].Own == Owner 
                          &
                          this[d.x, d.y + 1].Own != Owner 
                          //& this[d.x + 1, d.y].Own != Owner &
                          //this[d.x - 1, d.y + 1].Own != Owner &
                          //this[d.x + 1, d.y - 1].Own != Owner &
                          //this[d.x, d.y - 1].Own != Owner

                                //+
                                //   d  +       
                        | this[d.x + 1, d.y].Blocked == false & this[d.x - 1, d.y - 1].Blocked == false &
                         this[d.x + 1, d.y].Own == Owner & this[d.x - 1, d.y - 1].Own == Owner &
                         this[d.x, d.y - 1].Own != Owner 

                         //& this[d.x - 1, d.y + 1].Own != Owner &
                         //this[d.x - 1, d.y].Own != Owner &
                         //this[d.x + 1, d.y - 1].Own != Owner 
                          //& this[d.x, d.y + 1].Own != Owner

                                //   d  +       
                                //+
                        | this[d.x + 1, d.y].Blocked == false & this[d.x - 1, d.y + 1].Blocked == false &
                         this[d.x + 1, d.y].Own == Owner & this[d.x - 1, d.y + 1].Own == Owner &
                         this[d.x, d.y + 1].Own != Owner 
                         //&
                         //this[d.x + 1, d.y + 1].Own != Owner &
                         //this[d.x - 1, d.y].Own != Owner &
                         //this[d.x - 1, d.y - 1].Own != Owner &
                         // this[d.x, d.y - 1].Own != Owner

                                //+   
                                //   d          
                                //   +
                        | this[d.x, d.y + 1].Blocked == false & this[d.x - 1, d.y - 1].Blocked == false &
                         this[d.x, d.y + 1].Own == Owner & this[d.x - 1, d.y - 1].Own == Owner &
                         this[d.x - 1, d.y].Own != Owner
                         //&
                         //this[d.x - 1, d.y + 1].Own != Owner &
                         //this[d.x, d.y - 1].Own != Owner &
                         //this[d.x + 1, d.y - 1].Own != Owner 
                          //& this[d.x + 1, d.y].Own != Owner

                                //   +
                                //   d          
                                //+   
                        | this[d.x, d.y - 1].Blocked == false & this[d.x - 1, d.y + 1].Blocked == false &
                        this[d.x, d.y - 1].Own == Owner & this[d.x - 1, d.y + 1].Own == Owner &
                        this[d.x - 1, d.y].Own != Owner 
                        //&
                        //this[d.x - 1, d.y - 1].Own != Owner &
                        //this[d.x + 1, d.y].Own != Owner &
                        //this[d.x + 1, d.y + 1].Own != Owner &
                        //this[d.x, d.y + 1].Own != Owner

                                //   +
                                //   d          
                                //      +
                        | this[d.x, d.y - 1].Blocked == false & this[d.x + 1, d.y + 1].Blocked == false &
                        this[d.x, d.y - 1].Own == Owner & this[d.x + 1, d.y + 1].Own == Owner &
                        this[d.x + 1, d.y].Own != Owner 
                        //&
                        //this[d.x, d.y + 1].Own != Owner &
                        //this[d.x - 1, d.y + 1].Own != Owner &
                        //this[d.x - 1, d.y].Own != Owner &
                        //this[d.x + 1, d.y - 1].Own != Owner

                                //      +
                                //   d          
                                //   +   
                        | this[d.x, d.y + 1].Blocked == false & this[d.x + 1, d.y - 1].Blocked == false &
                        this[d.x, d.y + 1].Own == Owner & this[d.x + 1, d.y - 1].Own == Owner &
                        this[d.x + 1, d.y].Own != Owner 
                        //&
                        //this[d.x - 1, d.y - 1].Own != Owner &
                        //this[d.x, d.y - 1].Own != Owner &
                        //this[d.x + 1, d.y + 1].Own != Owner &
                        //this[d.x - 1, d.y].Own != Owner
#endregion
);
#if DEBUG
            Dot[] ad = qry.ToArray();
#endif
            foreach (Dot d in qry)
            {
                //делаем ход
                int result_last_move = MakeMove(d, Owner);
#if DEBUG
                //if (f.chkMove.Checked) Pause();
#endif
                //-----------------------------------
                if (result_last_move != 0 & this[d.x, d.y].Blocked == false)
                {
                    UndoMove(d);
                    d.CountBlockedDots = result_last_move;
                    happy_dots.Add(d);
                    //return d;
                }
                UndoMove(d);
            }

            //выбрать точку, которая максимально окружит
            var x = happy_dots.Where(dd => 
                    dd.CountBlockedDots == happy_dots.Max(dt => dt.CountBlockedDots));

            return x.Count() > 0 ? x.First() : null;

        }

        private Dot ОбщаяТочкаSNWE(Dot d1, Dot d2)//*1d1* 
        {
            return NeiborDotsSNWE(d1).Intersect(NeiborDotsSNWE(d2), new DotEq()).FirstOrDefault();
        }
        private List<Dot> ОбщаяТочка(Dot d1, Dot d2)
        {
            return NeiborDots(d1).Intersect(NeiborDots(d2), new DotEq()).ToList();
        }
        private Dot CheckMove(int Owner)
        {
            List<Dot> happy_dots = new List<Dot>();
            var qry = from Dot d1 in this
                      where d1.Own == Owner
                      from Dot d2 in this
                      where
                            d2.Own == Owner
                            && d2.IndexRelation == d1.IndexRelation
                            && Distance(d1, d2) > 2
                            && Distance(d1, d2) < 3
                            && ОбщаяТочка(d1, d2).Where(dt => dt.Own == Owner).Count() == 0
                            ||
                            d2.Own == Owner
                            && d2.IndexRelation == d1.IndexRelation
                            && Distance(d1, d2) == 2
                      from Dot d in this
                      where d.ValidMove && Distance(d, d1) < 2 && Distance(d, d2) < 2 
                                && NeiborDotsSNWE(d).Where(dt=>dt.Own==Owner).Count() <= 2
                      select d;
            
            foreach (Dot d in qry.Distinct(new DotEq()).ToList())
            {
                //делаем ход
                int result_last_move = MakeMove(d, Owner);
#if DEBUG
                //if (f.chkMove.Checked) Pause();
#endif
                //-----------------------------------
                if (result_last_move != 0 & this[d.x, d.y].Blocked == false)
                {
                    UndoMove(d);
                    d.CountBlockedDots = result_last_move;
                    happy_dots.Add(d);
                    //break;
                }
                UndoMove(d);
            }

            //выбрать точку, которая максимально окружит
            var x = happy_dots.Where(dd =>
                    dd.CountBlockedDots == happy_dots.Max(dt => dt.CountBlockedDots));

            return x.Count() > 0 ? x.First() : null;

        }

        private Dot CheckPatternVilkaNextMove(int Owner)
        {
            var qry = Board_NotEmptyNonBlockedDots.Where(dt => dt.Own == Owner);
            Dot dot_ptn;
            if (qry.Count() != 0)
            {
                foreach (Dot d in qry)
                {
                    foreach (Dot dot_move in NeiborDots(d))
                    {
                        if (dot_move.ValidMove)
                        {
                            //делаем ход
                            int result_last_move = MakeMove(dot_move, Owner);
                            int pl = Owner == 2 ? 1 : 2;
                            Dot dt = CheckMove(pl); // проверка чтобы не попасть в капкан
                            if (dt != null)
                            {
                                UndoMove(dot_move);
                                continue;
                            }
                            dot_ptn = CheckPattern_vilochka(d.Own);
                            //-----------------------------------
                            if (dot_ptn != null & result_last_move == 0)
                            {
                                UndoMove(dot_move);
                                return dot_move;
                            }
                            UndoMove(dot_move);
                        }
                    }
                }
            }
            return null;
        }
        private int iNumberPattern;
        // * *
        // + m +
        private List<Dot> Проверка1(int Owner) 
        {
            var qry = from Dot d1 in Board_NotEmptyNonBlockedDots.Where(dt => dt.Own == Owner)
                      where d1.Own == Owner && !d1.Blocked
                      from Dot d2 in Board_NotEmptyNonBlockedDots.Where(dt => dt.Own == Owner)
                      where d2.Own == Owner && !d2.Blocked
                              && Distance(d1, d2) == 1
                      from Dot de1 in Board_NotEmptyNonBlockedDots.Where(dt => dt.Own != Owner)
                      where de1.Own != Owner && Distance(de1, d1) == 1
                            && Distance(de1, d2) == 1.4f
                      from Dot de2 in Board_NotEmptyNonBlockedDots.Where(dt => dt.Own != Owner)
                      where de1.Own != Owner && Distance(de2, d2) == 1.4f
                            && Distance(de2, de1) == 2
                      from Dot dm in EmptyNeibourDots(Owner)
                      where dm.ValidMove && Distance(dm, d1) == 1.4f
                            && Distance(dm, d2) == 1
                            && Distance(dm, de1) == 1
                            && Distance(dm, de2) == 1
                      select dm;
            List<Dot> ld = qry.Distinct(new DotEq()).ToList();
            foreach (Dot d in ld) d.Tag = "Проверка1(" + Owner + ")";
            return ld;
        }

        private Dot CheckPattern_vilochka(int Owner)
        {
            int enemy_own = Owner == 1 ? 2 : 1;
            var get_non_blocked = from Dot d in this where d.Blocked == false select d; //получить коллекцию незаблокированных точек

            //паттерн на диагональное расположение точек         *d   
            //                                                *  +  
            //                                             *  +  *               
            iNumberPattern = 1;

            var pat1 = from Dot d in get_non_blocked
                       where d.Own == Owner &&
                           this[d.x - 1, d.y + 1].Own == Owner & this[d.x - 1, d.y + 1].Blocked == false
                           & this[d.x + 1, d.y - 1].Own == Owner & this[d.x + 1, d.y - 1].Blocked == false
                           & this[d.x + 1, d.y].Own == enemy_own & this[d.x + 1, d.y].Blocked == false
                           & this[d.x, d.y + 1].Own == enemy_own & this[d.x, d.y + 1].Blocked == false
                           & this[d.x + 1, d.y + 1].Own == 0 & this[d.x + 1, d.y + 1].Blocked == false
                           & this[d.x + 2, d.y].Own == 0 & this[d.x + 2, d.y].Blocked == false
                           & this[d.x, d.y + 2].Own == 0 & this[d.x, d.y + 2].Blocked == false
                           & this[d.x + 2, d.y + 1].Own != enemy_own & this[d.x + 2, d.y + 1].Blocked == false
                           & this[d.x + 1, d.y + 2].Own != enemy_own & this[d.x + 1, d.y + 2].Blocked == false
                       select d;
            if (pat1.Count() > 0) return new Dot(pat1.First().x + 1, pat1.First().y + 1);
            //паттерн на диагональное расположение точек    *  +  *d   
            //                                              +  *    
            //                                              *       

            var pat1_2 = from Dot d in get_non_blocked
                         where d.Own == Owner &&
                             this[d.x + 1, d.y - 1].Own == Owner & this[d.x + 1, d.y - 1].Blocked == false
                             & this[d.x - 1, d.y + 1].Own == Owner & this[d.x - 1, d.y + 1].Blocked == false
                             & this[d.x - 1, d.y].Own == enemy_own & this[d.x - 1, d.y].Blocked == false
                             & this[d.x, d.y - 1].Own == enemy_own & this[d.x, d.y - 1].Blocked == false
                             & this[d.x - 1, d.y - 1].Own == 0 & this[d.x - 1, d.y - 1].Blocked == false
                             & this[d.x - 2, d.y].Own == 0 & this[d.x - 2, d.y].Blocked == false
                             & this[d.x, d.y - 2].Own == 0 & this[d.x, d.y - 2].Blocked == false
                             & this[d.x - 2, d.y - 1].Own != enemy_own & this[d.x - 2, d.y - 1].Blocked == false
                             & this[d.x - 1, d.y - 2].Own != enemy_own & this[d.x - 1, d.y - 2].Blocked == false
                         select d;
            if (pat1_2.Count() > 0) return new Dot(pat1_2.First().x - 1, pat1_2.First().y - 1);

            //============================================================================================================== 
            //паттерн на диагональное расположение точек    *                   
            //                                              +  d    
            //                                              m  +  * 
            var pat1_3 = from Dot d in get_non_blocked
                         where d.Own == Owner &&
                             this[d.x + 1, d.y + 1].Own == Owner & this[d.x + 1, d.y + 1].Blocked == false
                             & this[d.x - 1, d.y - 1].Own == Owner & this[d.x - 1, d.y - 1].Blocked == false
                             & this[d.x - 1, d.y].Own == enemy_own & this[d.x - 1, d.y].Blocked == false
                             & this[d.x, d.y + 1].Own == enemy_own & this[d.x, d.y + 1].Blocked == false
                             & this[d.x - 1, d.y + 1].Own == 0 & this[d.x - 1, d.y + 1].Blocked == false
                             & this[d.x, d.y + 2].Own == 0 & this[d.x, d.y + 2].Blocked == false
                             & this[d.x - 2, d.y].Own == 0 & this[d.x - 2, d.y].Blocked == false
                             & this[d.x - 1, d.y + 2].Own != enemy_own & this[d.x - 1, d.y + 2].Blocked == false
                             & this[d.x - 2, d.y + 1].Own != enemy_own & this[d.x - 2, d.y + 1].Blocked == false
                         select d;
            if (pat1_3.Count() > 0) return new Dot(pat1_3.First().x - 1, pat1_3.First().y + 1);
            //180 Rotate=========================================================================================================== 
            //паттерн на диагональное расположение точек    *  +  m   
            //                                                 d  +  
            //                                                    * 
            var pat1_4 = from Dot d in get_non_blocked
                         where d.Own == Owner &&
                             this[d.x - 1, d.y - 1].Own == Owner & this[d.x - 1, d.y - 1].Blocked == false
                             & this[d.x + 1, d.y + 1].Own == Owner & this[d.x + 1, d.y + 1].Blocked == false
                             & this[d.x + 1, d.y].Own == enemy_own & this[d.x + 1, d.y].Blocked == false
                             & this[d.x, d.y - 1].Own == enemy_own & this[d.x, d.y - 1].Blocked == false
                             & this[d.x + 1, d.y - 1].Own == 0 & this[d.x + 1, d.y - 1].Blocked == false
                             & this[d.x, d.y - 2].Own == 0 & this[d.x, d.y - 2].Blocked == false
                             & this[d.x + 2, d.y].Own == 0 & this[d.x + 2, d.y].Blocked == false
                             & this[d.x + 1, d.y - 2].Own != enemy_own & this[d.x + 1, d.y - 2].Blocked == false
                             & this[d.x + 2, d.y - 1].Own != enemy_own & this[d.x + 2, d.y - 1].Blocked == false
                         select d;
            if (pat1_4.Count() > 0) return new Dot(pat1_4.First().x + 1, pat1_4.First().y - 1);
            //**********************************************************************************        

            //     *     *
            //  *  +  *  +   
            //  *        m
            //     * 
            iNumberPattern = 938;
            var pat938 = from Dot d in get_non_blocked
                         where d.Own == Owner &&
                             this[d.x + 1, d.y - 1].Own == Owner & this[d.x + 1, d.y - 1].Blocked == false
                             & this[d.x + 2, d.y].Own == 0 & this[d.x + 2, d.y].Blocked == false
                             & this[d.x + 1, d.y].Own == enemy_own & this[d.x + 1, d.y].Blocked == false
                             & this[d.x, d.y + 1].Own == enemy_own & this[d.x, d.y + 1].Blocked == false
                             & this[d.x - 1, d.y].Own == Owner & this[d.x - 1, d.y].Blocked == false
                             & this[d.x - 2, d.y + 1].Own == Owner & this[d.x - 2, d.y + 1].Blocked == false
                             & this[d.x - 1, d.y + 2].Own == Owner & this[d.x - 1, d.y + 2].Blocked == false
                             & this[d.x, d.y + 2].Own == 0 & this[d.x, d.y + 2].Blocked == false
                             & this[d.x + 1, d.y + 1].Own == 0 & this[d.x + 1, d.y + 1].Blocked == false
                             & this[d.x + 1, d.y + 2].Own == 0 & this[d.x + 1, d.y + 2].Blocked == false
                             & this[d.x + 2, d.y + 1].Own == 0 & this[d.x + 2, d.y + 1].Blocked == false
                         select d;
            if (pat938.Count() > 0) return new Dot(pat938.First().x + 1, pat938.First().y + 1);
            //180 Rotate=========================================================================================================== 
            var pat938_2 = from Dot d in get_non_blocked
                           where d.Own == Owner &&
                               this[d.x - 1, d.y + 1].Own == Owner & this[d.x - 1, d.y + 1].Blocked == false
                               & this[d.x - 2, d.y].Own == 0 & this[d.x - 2, d.y].Blocked == false
                               & this[d.x - 1, d.y].Own == enemy_own & this[d.x - 1, d.y].Blocked == false
                               & this[d.x, d.y - 1].Own == enemy_own & this[d.x, d.y - 1].Blocked == false
                               & this[d.x + 1, d.y].Own == Owner & this[d.x + 1, d.y].Blocked == false
                               & this[d.x + 2, d.y - 1].Own == Owner & this[d.x + 2, d.y - 1].Blocked == false
                               & this[d.x + 1, d.y - 2].Own == Owner & this[d.x + 1, d.y - 2].Blocked == false
                               & this[d.x, d.y - 2].Own == 0 & this[d.x, d.y - 2].Blocked == false
                               & this[d.x - 1, d.y - 1].Own == 0 & this[d.x - 1, d.y - 1].Blocked == false
                               & this[d.x - 1, d.y - 2].Own == 0 & this[d.x - 1, d.y - 2].Blocked == false
                               & this[d.x - 2, d.y - 1].Own == 0 & this[d.x - 2, d.y - 1].Blocked == false
                           select d;
            if (pat938_2.Count() > 0) return new Dot(pat938_2.First().x - 1, pat938_2.First().y - 1);
            //--------------Rotate on 90-----------------------------------
            var pat938_2_3 = from Dot d in get_non_blocked
                             where d.Own == Owner &&
                                 d.x + 1 < BoardWidth && d.x - 1 > -1 &&
                                 d.y + 1 < BoardHeight && d.y - 1 > -1 &&
                                 this[d.x + 1, d.y - 1].Own == Owner & this[d.x + 1, d.y - 1].Blocked == false
                                 & this[d.x, d.y - 2].Own == 0 & this[d.x, d.y - 2].Blocked == false
                                 & this[d.x, d.y - 1].Own == enemy_own & this[d.x, d.y - 1].Blocked == false
                                 & this[d.x - 1, d.y].Own == enemy_own & this[d.x - 1, d.y].Blocked == false
                                 & this[d.x, d.y + 1].Own == Owner & this[d.x, d.y + 1].Blocked == false
                                 & this[d.x - 1, d.y + 2].Own == Owner & this[d.x - 1, d.y + 2].Blocked == false
                                 & this[d.x - 2, d.y + 1].Own == Owner & this[d.x - 2, d.y + 1].Blocked == false
                                 & this[d.x - 2, d.y].Own == 0 & this[d.x - 2, d.y].Blocked == false
                                 & this[d.x - 1, d.y - 1].Own == 0 & this[d.x - 1, d.y - 1].Blocked == false
                                 & this[d.x - 2, d.y - 1].Own == 0 & this[d.x - 2, d.y - 1].Blocked == false
                                 & this[d.x - 1, d.y - 2].Own == 0 & this[d.x - 1, d.y - 2].Blocked == false
                             select d;
            if (pat938_2_3.Count() > 0) return new Dot(pat938_2_3.First().x - 1, pat938_2_3.First().y - 1);
            //--------------Rotate on 90 -2-----------------------------------
            var pat938_2_3_4 = from Dot d in get_non_blocked
                               where d.Own == Owner
                                   & this[d.x - 1, d.y + 1].Own == Owner & this[d.x - 1, d.y + 1].Blocked == false
                                   & this[d.x, d.y + 2].Own == 0 & this[d.x, d.y + 2].Blocked == false
                                   & this[d.x, d.y + 1].Own == enemy_own & this[d.x, d.y + 1].Blocked == false
                                   & this[d.x + 1, d.y].Own == enemy_own & this[d.x + 1, d.y].Blocked == false
                                   & this[d.x, d.y - 1].Own == Owner & this[d.x, d.y - 1].Blocked == false
                                   & this[d.x + 1, d.y - 2].Own == Owner & this[d.x + 1, d.y - 2].Blocked == false
                                   & this[d.x + 2, d.y - 1].Own == Owner & this[d.x + 2, d.y - 1].Blocked == false
                                   & this[d.x + 2, d.y].Own == 0 & this[d.x + 2, d.y].Blocked == false
                                   & this[d.x + 1, d.y + 1].Own == 0 & this[d.x + 1, d.y + 1].Blocked == false
                                   & this[d.x + 2, d.y + 1].Own == 0 & this[d.x + 2, d.y + 1].Blocked == false
                                   & this[d.x + 1, d.y + 2].Own == 0 & this[d.x + 1, d.y + 2].Blocked == false
                               select d;
            if (pat938_2_3_4.Count() > 0) return new Dot(pat938_2_3_4.First().x + 1, pat938_2_3_4.First().y + 1);
            //============================================================================================================== 

            //===========ВИЛОЧКА=================================================================================================== 
            //     *   
            //     +  
            //  +  *
            //  *
            iNumberPattern = 670;
            var pat670 = from Dot d in get_non_blocked
                         where d.Own == Owner
                             & this[d.x, d.y + 2].Own == Owner
                             & this[d.x, d.y + 1].Own == enemy_own
                             & this[d.x + 1, d.y - 1].Own == Owner
                             & this[d.x + 1, d.y].Own == enemy_own
                             & this[d.x + 1, d.y + 1].Own == 0
                             & this[d.x + 2, d.y].Own == 0
                             & this[d.x - 1, d.y + 1].Own == 0
                             & this[d.x + 2, d.y - 1].Own != enemy_own
                             & this[d.x, d.y - 1].Own != enemy_own
                             & this[d.x + 2, d.y + 1].Own != enemy_own
                             & this[d.x + 1, d.y + 2].Own != enemy_own
                             & this[d.x - 1, d.y].Own != enemy_own
                         select d;
            if (pat670.Count() > 0) return new Dot(pat670.First().x + 1, pat670.First().y + 1);
            //180 Rotate=========================================================================================================== 
            var pat670_2 = from Dot d in get_non_blocked
                           where d.Own == Owner
                               & this[d.x, d.y - 2].Own == Owner
                               & this[d.x, d.y - 1].Own == enemy_own
                               & this[d.x - 1, d.y + 1].Own == Owner
                               & this[d.x - 1, d.y].Own == enemy_own
                               & this[d.x - 1, d.y - 1].Own == 0
                               & this[d.x - 2, d.y].Own == 0
                               & this[d.x + 1, d.y - 1].Own == 0
                               & this[d.x - 2, d.y + 1].Own != enemy_own
                               & this[d.x, d.y + 1].Own != enemy_own
                               & this[d.x - 2, d.y - 1].Own != enemy_own
                               & this[d.x - 1, d.y - 2].Own != enemy_own
                               & this[d.x + 1, d.y].Own != enemy_own
                           select d;
            if (pat670_2.Count() > 0) return new Dot(pat670_2.First().x - 1, pat670_2.First().y - 1);
            //--------------Rotate on 90-----------------------------------
            var pat670_2_3 = from Dot d in get_non_blocked
                             where d.Own == Owner
                                 & this[d.x - 2, d.y].Own == Owner
                                 & this[d.x - 1, d.y].Own == enemy_own
                                 & this[d.x + 1, d.y - 1].Own == Owner
                                 & this[d.x, d.y - 1].Own == enemy_own
                                 & this[d.x - 1, d.y - 1].Own == 0
                                 & this[d.x, d.y - 2].Own == 0
                                 & this[d.x - 1, d.y + 1].Own == 0
                                 & this[d.x + 1, d.y - 2].Own != enemy_own
                                 & this[d.x + 1, d.y].Own != enemy_own
                                 & this[d.x - 1, d.y - 2].Own != enemy_own
                                 & this[d.x - 2, d.y - 1].Own != enemy_own
                                 & this[d.x, d.y + 1].Own != enemy_own
                             select d;
            if (pat670_2_3.Count() > 0) return new Dot(pat670_2_3.First().x - 1, pat670_2_3.First().y - 1);
            //--------------Rotate on 90 -2-----------------------------------
            var pat670_2_3_4 = from Dot d in get_non_blocked
                               where d.Own == Owner
                                   & this[d.x + 2, d.y].Own == Owner
                                   & this[d.x + 1, d.y].Own == enemy_own
                                   & this[d.x - 1, d.y + 1].Own == Owner
                                   & this[d.x, d.y + 1].Own == enemy_own
                                   & this[d.x + 1, d.y + 1].Own == 0
                                   & this[d.x, d.y + 2].Own == 0
                                   & this[d.x + 1, d.y - 1].Own == 0
                                   & this[d.x - 1, d.y + 2].Own != enemy_own
                                   & this[d.x - 1, d.y].Own != enemy_own
                                   & this[d.x + 1, d.y + 2].Own != enemy_own
                                   & this[d.x + 2, d.y + 1].Own != enemy_own
                                   & this[d.x, d.y - 1].Own != enemy_own
                               select d;
            if (pat670_2_3_4.Count() > 0) return new Dot(pat670_2_3_4.First().x + 1, pat670_2_3_4.First().y + 1);
            //============================================================================================================== 
            iNumberPattern = 30;
            var pat30 = from Dot d in get_non_blocked
                        where d.Own == Owner
                            & this[d.x - 1, d.y + 1].Own == Owner
                            & this[d.x - 1, d.y].Own == enemy_own
                            & this[d.x, d.y - 1].Own == enemy_own
                            & this[d.x, d.y - 2].Own == Owner
                            & this[d.x + 1, d.y - 1].Own == 0
                            & this[d.x - 1, d.y - 1].Own == 0
                            & this[d.x - 1, d.y - 2].Own == 0
                            & this[d.x - 2, d.y - 1].Own == 0
                            & this[d.x - 2, d.y].Own == 0
                        select d;
            if (pat30.Count() > 0) return new Dot(pat30.First().x - 1, pat30.First().y - 1);
            //180 Rotate=========================================================================================================== 
            var pat30_2 = from Dot d in get_non_blocked
                          where d.Own == Owner
                              & this[d.x + 1, d.y - 1].Own == Owner
                              & this[d.x + 1, d.y].Own == enemy_own
                              & this[d.x, d.y + 1].Own == enemy_own
                              & this[d.x, d.y + 2].Own == Owner
                              & this[d.x - 1, d.y + 1].Own == 0
                              & this[d.x + 1, d.y + 1].Own == 0
                              & this[d.x + 1, d.y + 2].Own == 0
                              & this[d.x + 2, d.y + 1].Own == 0
                              & this[d.x + 2, d.y].Own == 0
                          select d;
            if (pat30_2.Count() > 0) return new Dot(pat30_2.First().x + 1, pat30_2.First().y + 1);
            //--------------Rotate on 90-----------------------------------
            var pat30_2_3 = from Dot d in get_non_blocked
                            where d.Own == Owner
                                & this[d.x - 1, d.y + 1].Own == Owner
                                & this[d.x, d.y + 1].Own == enemy_own
                                & this[d.x + 1, d.y].Own == enemy_own
                                & this[d.x + 2, d.y].Own == Owner
                                & this[d.x + 1, d.y - 1].Own == 0
                                & this[d.x + 1, d.y + 1].Own == 0
                                & this[d.x + 2, d.y + 1].Own == 0
                                & this[d.x + 1, d.y + 2].Own == 0
                                & this[d.x, d.y + 2].Own == 0
                            select d;
            if (pat30_2_3.Count() > 0) return new Dot(pat30_2_3.First().x + 1, pat30_2_3.First().y + 1);
            //--------------Rotate on 90 -2-----------------------------------
            var pat30_2_3_4 = from Dot d in get_non_blocked
                              where d.Own == Owner
                                  & this[d.x + 1, d.y - 1].Own == Owner
                                  & this[d.x, d.y - 1].Own == enemy_own
                                  & this[d.x - 1, d.y].Own == enemy_own
                                  & this[d.x - 2, d.y].Own == Owner
                                  & this[d.x - 1, d.y + 1].Own == 0
                                  & this[d.x - 1, d.y - 1].Own == 0
                                  & this[d.x - 2, d.y - 1].Own == 0
                                  & this[d.x - 1, d.y - 2].Own == 0
                                  & this[d.x, d.y - 2].Own == 0
                              select d;
            if (pat30_2_3_4.Count() > 0) return new Dot(pat30_2_3_4.First().x - 1, pat30_2_3_4.First().y - 1);
            //============================================================================================================== 
            iNumberPattern = 295;
            var pat295 = from Dot d in get_non_blocked
                         where d.Own == Owner
                             & this[d.x - 1, d.y].Own == enemy_own
                             & this[d.x, d.y + 1].Own == enemy_own
                             & this[d.x, d.y + 2].Own == enemy_own
                             & this[d.x + 1, d.y + 3].Own == enemy_own
                             & this[d.x + 2, d.y + 2].Own == enemy_own
                             & this[d.x, d.y - 1].Own == 0
                             & this[d.x + 1, d.y - 1].Own == 0
                             & this[d.x + 1, d.y].Own == 0
                             & this[d.x + 2, d.y].Own == 0
                             & this[d.x + 2, d.y + 1].Own == 0
                             & this[d.x + 1, d.y + 1].Own != enemy_own
                             & this[d.x + 1, d.y + 2].Own != enemy_own
                         select d;
            if (pat295.Count() > 0) return new Dot(pat295.First().x + 1, pat295.First().y);
            //180 Rotate=========================================================================================================== 
            var pat295_2 = from Dot d in get_non_blocked
                           where d.Own == Owner
                               & this[d.x + 1, d.y].Own == enemy_own
                               & this[d.x, d.y - 1].Own == enemy_own
                               & this[d.x, d.y - 2].Own == enemy_own
                               & this[d.x - 1, d.y - 3].Own == enemy_own
                               & this[d.x - 2, d.y - 2].Own == enemy_own
                               & this[d.x, d.y + 1].Own == 0
                               & this[d.x - 1, d.y + 1].Own == 0
                               & this[d.x - 1, d.y].Own == 0
                               & this[d.x - 2, d.y].Own == 0
                               & this[d.x - 2, d.y - 1].Own == 0
                               & this[d.x - 1, d.y - 1].Own != enemy_own
                               & this[d.x - 1, d.y - 2].Own != enemy_own
                           select d;
            if (pat295_2.Count() > 0) return new Dot(pat295_2.First().x - 1, pat295_2.First().y);
            //--------------Rotate on 90-----------------------------------
            var pat295_2_3 = from Dot d in get_non_blocked
                             where d.Own == Owner
                                 & this[d.x, d.y + 1].Own == enemy_own
                                 & this[d.x - 1, d.y].Own == enemy_own
                                 & this[d.x - 2, d.y].Own == enemy_own
                                 & this[d.x - 3, d.y - 1].Own == enemy_own
                                 & this[d.x - 2, d.y - 2].Own == enemy_own
                                 & this[d.x + 1, d.y].Own == 0
                                 & this[d.x + 1, d.y - 1].Own == 0
                                 & this[d.x, d.y - 1].Own == 0
                                 & this[d.x, d.y - 2].Own == 0
                                 & this[d.x - 1, d.y - 2].Own == 0
                                 & this[d.x - 1, d.y - 1].Own != enemy_own
                                 & this[d.x - 2, d.y - 1].Own != enemy_own
                             select d;
            if (pat295_2_3.Count() > 0) return new Dot(pat295_2_3.First().x, pat295_2_3.First().y - 1);
            //--------------Rotate on 90 -2-----------------------------------
            var pat295_2_3_4 = from Dot d in get_non_blocked
                               where d.Own == Owner
                                   & this[d.x, d.y - 1].Own == enemy_own
                                   & this[d.x + 1, d.y].Own == enemy_own
                                   & this[d.x + 2, d.y].Own == enemy_own
                                   & this[d.x + 3, d.y + 1].Own == enemy_own
                                   & this[d.x + 2, d.y + 2].Own == enemy_own
                                   & this[d.x - 1, d.y].Own == 0
                                   & this[d.x - 1, d.y + 1].Own == 0
                                   & this[d.x, d.y + 1].Own == 0
                                   & this[d.x, d.y + 2].Own == 0
                                   & this[d.x + 1, d.y + 2].Own == 0
                                   & this[d.x + 1, d.y + 1].Own != enemy_own
                                   & this[d.x + 2, d.y + 1].Own != enemy_own
                               select d;
            if (pat295_2_3_4.Count() > 0) return new Dot(pat295_2_3_4.First().x, pat295_2_3_4.First().y + 1);
            //============================================================================================================== 
            iNumberPattern = 968;
            var pat968 = from Dot d in get_non_blocked
                         where d.Own == Owner
& this[d.x + 1, d.y - 2].Own == Owner
& this[d.x + 2, d.y - 1].Own == 0
& this[d.x + 2, d.y].Own == 0
& this[d.x + 2, d.y + 1].Own == 0
& this[d.x + 1, d.y + 2].Own == Owner
& this[d.x + 1, d.y + 1].Own == enemy_own
& this[d.x, d.y + 1].Own == Owner
& this[d.x, d.y - 1].Own == Owner
& this[d.x + 1, d.y - 1].Own == enemy_own
& this[d.x + 1, d.y].Own == 0
                         select d;
            if (pat968.Count() > 0) return new Dot(pat968.First().x + 1, pat968.First().y);
            //180 Rotate=========================================================================================================== 
            var pat968_2 = from Dot d in get_non_blocked
                           where d.Own == Owner
& this[d.x - 1, d.y + 2].Own == Owner
& this[d.x - 2, d.y + 1].Own == 0
& this[d.x - 2, d.y].Own == 0
& this[d.x - 2, d.y - 1].Own == 0
& this[d.x - 1, d.y - 2].Own == Owner
& this[d.x - 1, d.y - 1].Own == enemy_own
& this[d.x, d.y - 1].Own == Owner
& this[d.x, d.y + 1].Own == Owner
& this[d.x - 1, d.y + 1].Own == enemy_own
& this[d.x - 1, d.y].Own == 0
                           select d;
            if (pat968_2.Count() > 0) return new Dot(pat968_2.First().x - 1, pat968_2.First().y);
            //--------------Rotate on 90-----------------------------------
            var pat968_2_3 = from Dot d in get_non_blocked
                             where d.Own == Owner
& this[d.x + 2, d.y - 1].Own == Owner
& this[d.x + 1, d.y - 2].Own == 0
& this[d.x, d.y - 2].Own == 0
& this[d.x - 1, d.y - 2].Own == 0
& this[d.x - 2, d.y - 1].Own == Owner
& this[d.x - 1, d.y - 1].Own == enemy_own
& this[d.x - 1, d.y].Own == Owner
& this[d.x + 1, d.y].Own == Owner
& this[d.x + 1, d.y - 1].Own == enemy_own
& this[d.x, d.y - 1].Own == 0
                             select d;
            if (pat968_2_3.Count() > 0) return new Dot(pat968_2_3.First().x, pat968_2_3.First().y - 1);
            //--------------Rotate on 90 -2-----------------------------------
            var pat968_2_3_4 = from Dot d in get_non_blocked
                               where d.Own == Owner
& this[d.x - 2, d.y + 1].Own == Owner
& this[d.x - 1, d.y + 2].Own == 0
& this[d.x, d.y + 2].Own == 0
& this[d.x + 1, d.y + 2].Own == 0
& this[d.x + 2, d.y + 1].Own == Owner
& this[d.x + 1, d.y + 1].Own == enemy_own
& this[d.x + 1, d.y].Own == Owner
& this[d.x - 1, d.y].Own == Owner
& this[d.x - 1, d.y + 1].Own == enemy_own
& this[d.x, d.y + 1].Own == 0
                               select d;
            if (pat968_2_3_4.Count() > 0) return new Dot(pat968_2_3_4.First().x, pat968_2_3_4.First().y + 1);
            //============================================================================================================== 
            iNumberPattern = 880;
            var pat880 = from Dot d in get_non_blocked
                         where d.Own == Owner
                             & this[d.x + 1, d.y + 2].Own == enemy_own & this[d.x + 1, d.y + 2].Blocked == false
                             & this[d.x + 1, d.y + 1].Own == Owner & this[d.x + 1, d.y + 1].Blocked == false
                             & this[d.x - 1, d.y - 1].Own == enemy_own & this[d.x - 1, d.y - 1].Blocked == false
                             & this[d.x, d.y + 1].Own == enemy_own & this[d.x, d.y + 1].Blocked == false
                             & this[d.x, d.y - 1].Own == 0 & this[d.x, d.y - 1].Blocked == false
                             & this[d.x + 1, d.y - 1].Own == 0 & this[d.x + 1, d.y - 1].Blocked == false
                             & this[d.x + 2, d.y].Own == 0 & this[d.x + 2, d.y].Blocked == false
                             & this[d.x + 1, d.y].Own == 0 & this[d.x + 1, d.y].Blocked == false
                             & this[d.x + 2, d.y + 1].Own == 0 & this[d.x + 2, d.y + 1].Blocked == false
                             & this[d.x - 2, d.y].Own == enemy_own & this[d.x - 2, d.y].Blocked == false
                             & this[d.x - 2, d.y + 1].Own == enemy_own & this[d.x - 2, d.y + 1].Blocked == false
                             & this[d.x - 1, d.y + 2].Own == enemy_own & this[d.x - 1, d.y + 2].Blocked == false
                             & this[d.x - 1, d.y].Own != enemy_own & this[d.x - 1, d.y].Blocked == false
                             & this[d.x - 1, d.y + 1].Own != enemy_own & this[d.x - 1, d.y + 1].Blocked == false
                         select d;
            if (pat880.Count() > 0) return new Dot(pat880.First().x + 1, pat880.First().y - 1);
            //180 Rotate=========================================================================================================== 
            var pat880_2 = from Dot d in get_non_blocked
                           where d.Own == Owner
                               & this[d.x - 1, d.y - 2].Own == enemy_own & this[d.x - 1, d.y - 2].Blocked == false
                               & this[d.x - 1, d.y - 1].Own == Owner & this[d.x - 1, d.y - 1].Blocked == false
                               & this[d.x + 1, d.y + 1].Own == enemy_own & this[d.x + 1, d.y + 1].Blocked == false
                               & this[d.x, d.y - 1].Own == enemy_own & this[d.x, d.y - 1].Blocked == false
                               & this[d.x, d.y + 1].Own == 0 & this[d.x, d.y + 1].Blocked == false
                               & this[d.x - 1, d.y + 1].Own == 0 & this[d.x - 1, d.y + 1].Blocked == false
                               & this[d.x - 2, d.y].Own == 0 & this[d.x - 2, d.y].Blocked == false
                               & this[d.x - 1, d.y].Own == 0 & this[d.x - 1, d.y].Blocked == false
                               & this[d.x - 2, d.y - 1].Own == 0 & this[d.x - 2, d.y - 1].Blocked == false
                               & this[d.x + 2, d.y].Own == enemy_own & this[d.x + 2, d.y].Blocked == false
                               & this[d.x + 2, d.y - 1].Own == enemy_own & this[d.x + 2, d.y - 1].Blocked == false
                               & this[d.x + 1, d.y - 2].Own == enemy_own & this[d.x + 1, d.y - 2].Blocked == false
                               & this[d.x + 1, d.y].Own != enemy_own & this[d.x + 1, d.y].Blocked == false
                               & this[d.x + 1, d.y - 1].Own != enemy_own & this[d.x + 1, d.y - 1].Blocked == false
                           select d;
            if (pat880_2.Count() > 0) return new Dot(pat880_2.First().x - 1, pat880_2.First().y + 1);
            //--------------Rotate on 90-----------------------------------
            var pat880_2_3 = from Dot d in get_non_blocked
                             where d.Own == Owner
                                 & this[d.x - 2, d.y - 1].Own == enemy_own & this[d.x - 2, d.y - 1].Blocked == false
                                 & this[d.x - 1, d.y - 1].Own == Owner & this[d.x - 1, d.y - 1].Blocked == false
                                 & this[d.x + 1, d.y + 1].Own == enemy_own & this[d.x + 1, d.y + 1].Blocked == false
                                 & this[d.x - 1, d.y].Own == enemy_own & this[d.x - 1, d.y].Blocked == false
                                 & this[d.x + 1, d.y].Own == 0 & this[d.x + 1, d.y].Blocked == false
                                 & this[d.x + 1, d.y - 1].Own == 0 & this[d.x + 1, d.y - 1].Blocked == false
                                 & this[d.x, d.y - 2].Own == 0 & this[d.x, d.y - 2].Blocked == false
                                 & this[d.x, d.y - 1].Own == 0 & this[d.x, d.y - 1].Blocked == false
                                 & this[d.x - 1, d.y - 2].Own == 0 & this[d.x - 1, d.y - 2].Blocked == false
                                 & this[d.x, d.y + 2].Own == enemy_own & this[d.x, d.y + 2].Blocked == false
                                 & this[d.x - 1, d.y + 2].Own == enemy_own & this[d.x - 1, d.y + 2].Blocked == false
                                 & this[d.x - 2, d.y + 1].Own == enemy_own & this[d.x - 2, d.y + 1].Blocked == false
                                 & this[d.x, d.y + 1].Own != enemy_own & this[d.x, d.y + 1].Blocked == false
                                 & this[d.x - 1, d.y + 1].Own != enemy_own & this[d.x - 1, d.y + 1].Blocked == false
                             select d;
            if (pat880_2_3.Count() > 0) return new Dot(pat880_2_3.First().x + 1, pat880_2_3.First().y - 1);
            //--------------Rotate on 90 -2-----------------------------------
            var pat880_2_3_4 = from Dot d in get_non_blocked
                               where d.Own == Owner
                                   & this[d.x + 2, d.y + 1].Own == enemy_own & this[d.x + 2, d.y + 1].Blocked == false
                                   & this[d.x + 1, d.y + 1].Own == Owner & this[d.x + 1, d.y + 1].Blocked == false
                                   & this[d.x - 1, d.y - 1].Own == enemy_own & this[d.x - 1, d.y - 1].Blocked == false
                                   & this[d.x + 1, d.y].Own == enemy_own & this[d.x + 1, d.y].Blocked == false
                                   & this[d.x - 1, d.y].Own == 0 & this[d.x - 1, d.y].Blocked == false
                                   & this[d.x - 1, d.y + 1].Own == 0 & this[d.x - 1, d.y + 1].Blocked == false
                                   & this[d.x, d.y + 2].Own == 0 & this[d.x, d.y + 2].Blocked == false
                                   & this[d.x, d.y + 1].Own == 0 & this[d.x, d.y + 1].Blocked == false
                                   & this[d.x + 1, d.y + 2].Own == 0 & this[d.x + 1, d.y + 2].Blocked == false
                                   & this[d.x, d.y - 2].Own == enemy_own & this[d.x, d.y - 2].Blocked == false
                                   & this[d.x + 1, d.y - 2].Own == enemy_own & this[d.x + 1, d.y - 2].Blocked == false
                                   & this[d.x + 2, d.y - 1].Own == enemy_own & this[d.x + 2, d.y - 1].Blocked == false
                                   & this[d.x, d.y - 1].Own != enemy_own & this[d.x, d.y - 1].Blocked == false
                                   & this[d.x + 1, d.y - 1].Own != enemy_own & this[d.x + 1, d.y - 1].Blocked == false
                               select d;
            if (pat880_2_3_4.Count() > 0) return new Dot(pat880_2_3_4.First().x - 1, pat880_2_3_4.First().y + 1);
            //============================================================================================================== 
            iNumberPattern = 775;
            var pat775 = from Dot d in get_non_blocked
                         where d.Own == Owner
                             & this[d.x - 2, d.y].Own == Owner & this[d.x - 2, d.y].Blocked == false
                             & this[d.x - 1, d.y - 1].Own == 0 & this[d.x - 1, d.y - 1].Blocked == false
                             & this[d.x - 1, d.y].Own == enemy_own & this[d.x - 1, d.y].Blocked == false
                             & this[d.x + 1, d.y + 1].Own == Owner & this[d.x + 1, d.y + 1].Blocked == false
                             & this[d.x - 1, d.y + 1].Own == 0 & this[d.x - 1, d.y + 1].Blocked == false
                             & this[d.x, d.y + 1].Own == enemy_own & this[d.x, d.y + 1].Blocked == false
                             & this[d.x, d.y + 2].Own == 0 & this[d.x, d.y + 2].Blocked == false
                             & this[d.x - 1, d.y + 2].Own == 0 & this[d.x - 1, d.y + 2].Blocked == false
                             & this[d.x - 2, d.y + 1].Own == 0 & this[d.x - 2, d.y + 1].Blocked == false
                             & this[d.x + 1, d.y].Own != enemy_own & this[d.x + 1, d.y].Blocked == false
                         select d;
            if (pat775.Count() > 0) return new Dot(pat775.First().x - 1, pat775.First().y + 1);
            //180 Rotate=========================================================================================================== 
            var pat775_2 = from Dot d in get_non_blocked
                           where d.Own == Owner
                               & this[d.x + 2, d.y].Own == Owner & this[d.x + 2, d.y].Blocked == false
                               & this[d.x + 1, d.y + 1].Own == 0 & this[d.x + 1, d.y + 1].Blocked == false
                               & this[d.x + 1, d.y].Own == enemy_own & this[d.x + 1, d.y].Blocked == false
                               & this[d.x - 1, d.y - 1].Own == Owner & this[d.x - 1, d.y - 1].Blocked == false
                               & this[d.x + 1, d.y - 1].Own == 0 & this[d.x + 1, d.y - 1].Blocked == false
                               & this[d.x, d.y - 1].Own == enemy_own & this[d.x, d.y - 1].Blocked == false
                               & this[d.x, d.y - 2].Own == 0 & this[d.x, d.y - 2].Blocked == false
                               & this[d.x + 1, d.y - 2].Own == 0 & this[d.x + 1, d.y - 2].Blocked == false
                               & this[d.x + 2, d.y - 1].Own == 0 & this[d.x + 2, d.y - 1].Blocked == false
                               & this[d.x - 1, d.y].Own != enemy_own & this[d.x - 1, d.y].Blocked == false
                           select d;
            if (pat775_2.Count() > 0) return new Dot(pat775_2.First().x + 1, pat775_2.First().y - 1);
            //--------------Rotate on 90-----------------------------------
            var pat775_2_3 = from Dot d in get_non_blocked
                             where d.Own == Owner
                                 & this[d.x, d.y + 2].Own == Owner & this[d.x, d.y + 2].Blocked == false
                                 & this[d.x + 1, d.y + 1].Own == 0 & this[d.x + 1, d.y + 1].Blocked == false
                                 & this[d.x, d.y + 1].Own == enemy_own & this[d.x, d.y + 1].Blocked == false
                                 & this[d.x - 1, d.y - 1].Own == Owner & this[d.x - 1, d.y - 1].Blocked == false
                                 & this[d.x - 1, d.y + 1].Own == 0 & this[d.x - 1, d.y + 1].Blocked == false
                                 & this[d.x - 1, d.y].Own == enemy_own & this[d.x - 1, d.y].Blocked == false
                                 & this[d.x - 2, d.y].Own == 0 & this[d.x - 2, d.y].Blocked == false
                                 & this[d.x - 2, d.y + 1].Own == 0 & this[d.x - 2, d.y + 1].Blocked == false
                                 & this[d.x - 1, d.y + 2].Own == 0 & this[d.x - 1, d.y + 2].Blocked == false
                                 & this[d.x, d.y - 1].Own != enemy_own & this[d.x, d.y - 1].Blocked == false
                             select d;
            if (pat775_2_3.Count() > 0) return new Dot(pat775_2_3.First().x - 1, pat775_2_3.First().y + 1);
            //--------------Rotate on 90 -2-----------------------------------
            var pat775_2_3_4 = from Dot d in get_non_blocked
                               where d.Own == Owner
                                   & this[d.x, d.y - 2].Own == Owner & this[d.x, d.y - 2].Blocked == false
                                   & this[d.x - 1, d.y - 1].Own == 0 & this[d.x - 1, d.y - 1].Blocked == false
                                   & this[d.x, d.y - 1].Own == enemy_own & this[d.x, d.y - 1].Blocked == false
                                   & this[d.x + 1, d.y + 1].Own == Owner & this[d.x + 1, d.y + 1].Blocked == false
                                   & this[d.x + 1, d.y - 1].Own == 0 & this[d.x + 1, d.y - 1].Blocked == false
                                   & this[d.x + 1, d.y].Own == enemy_own & this[d.x + 1, d.y].Blocked == false
                                   & this[d.x + 2, d.y].Own == 0 & this[d.x + 2, d.y].Blocked == false
                                   & this[d.x + 2, d.y - 1].Own == 0 & this[d.x + 2, d.y - 1].Blocked == false
                                   & this[d.x + 1, d.y - 2].Own == 0 & this[d.x + 1, d.y - 2].Blocked == false
                                   & this[d.x, d.y + 1].Own != enemy_own & this[d.x, d.y + 1].Blocked == false
                               select d;
            if (pat775_2_3_4.Count() > 0) return new Dot(pat775_2_3_4.First().x + 1, pat775_2_3_4.First().y - 1);
            //============================================================================================================== 
            iNumberPattern = 685;
            var pat685 = from Dot d in get_non_blocked
                         where d.Own == Owner
                             & this[d.x - 2, d.y + 3].Own == Owner & this[d.x - 2, d.y + 3].Blocked == false
                             & this[d.x - 1, d.y + 2].Own == Owner & this[d.x - 1, d.y + 2].Blocked == false
                             & this[d.x, d.y + 3].Own == Owner & this[d.x, d.y + 3].Blocked == false
                             & this[d.x + 1, d.y + 2].Own == Owner & this[d.x + 1, d.y + 2].Blocked == false
                             & this[d.x + 1, d.y + 1].Own == Owner & this[d.x + 1, d.y + 1].Blocked == false
                             & this[d.x - 1, d.y].Own == 0 & this[d.x - 1, d.y].Blocked == false
                             & this[d.x - 2, d.y].Own == 0 & this[d.x - 2, d.y].Blocked == false
                             & this[d.x - 2, d.y + 1].Own == 0 & this[d.x - 2, d.y + 1].Blocked == false
                             & this[d.x - 3, d.y + 2].Own == 0 & this[d.x - 3, d.y + 2].Blocked == false
                             & this[d.x - 3, d.y + 1].Own == 0 & this[d.x - 3, d.y + 1].Blocked == false
                             & this[d.x - 2, d.y + 2].Own == enemy_own & this[d.x - 2, d.y + 2].Blocked == false
                             & this[d.x - 1, d.y + 1].Own == enemy_own & this[d.x - 1, d.y + 1].Blocked == false
                             & this[d.x - 1, d.y - 1].Own == 0 & this[d.x - 1, d.y - 1].Blocked == false
                         select d;
            if (pat685.Count() > 0) return new Dot(pat685.First().x - 2, pat685.First().y + 1);
            //180 Rotate=========================================================================================================== 
            var pat685_2 = from Dot d in get_non_blocked
                           where d.Own == Owner
                               & this[d.x + 2, d.y - 3].Own == Owner & this[d.x + 2, d.y - 3].Blocked == false
                               & this[d.x + 1, d.y - 2].Own == Owner & this[d.x + 1, d.y - 2].Blocked == false
                               & this[d.x, d.y - 3].Own == Owner & this[d.x, d.y - 3].Blocked == false
                               & this[d.x - 1, d.y - 2].Own == Owner & this[d.x - 1, d.y - 2].Blocked == false
                               & this[d.x - 1, d.y - 1].Own == Owner & this[d.x - 1, d.y - 1].Blocked == false
                               & this[d.x + 1, d.y].Own == 0 & this[d.x + 1, d.y].Blocked == false
                               & this[d.x + 2, d.y].Own == 0 & this[d.x + 2, d.y].Blocked == false
                               & this[d.x + 2, d.y - 1].Own == 0 & this[d.x + 2, d.y - 1].Blocked == false
                               & this[d.x + 3, d.y - 2].Own == 0 & this[d.x + 3, d.y - 2].Blocked == false
                               & this[d.x + 3, d.y - 1].Own == 0 & this[d.x + 3, d.y - 1].Blocked == false
                               & this[d.x + 2, d.y - 2].Own == enemy_own & this[d.x + 2, d.y - 2].Blocked == false
                               & this[d.x + 1, d.y - 1].Own == enemy_own & this[d.x + 1, d.y - 1].Blocked == false
                               & this[d.x + 1, d.y + 1].Own == 0 & this[d.x + 1, d.y + 1].Blocked == false
                           select d;
            if (pat685_2.Count() > 0) return new Dot(pat685_2.First().x + 2, pat685_2.First().y - 1);
            //--------------Rotate on 90-----------------------------------
            var pat685_2_3 = from Dot d in get_non_blocked
                             where d.Own == Owner
                                 & this[d.x - 3, d.y + 2].Own == Owner & this[d.x - 3, d.y + 2].Blocked == false
                                 & this[d.x - 2, d.y + 1].Own == Owner & this[d.x - 2, d.y + 1].Blocked == false
                                 & this[d.x - 3, d.y].Own == Owner & this[d.x - 3, d.y].Blocked == false
                                 & this[d.x - 2, d.y - 1].Own == Owner & this[d.x - 2, d.y - 1].Blocked == false
                                 & this[d.x - 1, d.y - 1].Own == Owner & this[d.x - 1, d.y - 1].Blocked == false
                                 & this[d.x, d.y + 1].Own == 0 & this[d.x, d.y + 1].Blocked == false
                                 & this[d.x, d.y + 2].Own == 0 & this[d.x, d.y + 2].Blocked == false
                                 & this[d.x - 1, d.y + 2].Own == 0 & this[d.x - 1, d.y + 2].Blocked == false
                                 & this[d.x - 2, d.y + 3].Own == 0 & this[d.x - 2, d.y + 3].Blocked == false
                                 & this[d.x - 1, d.y + 3].Own == 0 & this[d.x - 1, d.y + 3].Blocked == false
                                 & this[d.x - 2, d.y + 2].Own == enemy_own & this[d.x - 2, d.y + 2].Blocked == false
                                 & this[d.x - 1, d.y + 1].Own == enemy_own & this[d.x - 1, d.y + 1].Blocked == false
                                 & this[d.x + 1, d.y + 1].Own == 0 & this[d.x + 1, d.y + 1].Blocked == false
                             select d;
            if (pat685_2_3.Count() > 0) return new Dot(pat685_2_3.First().x - 1, pat685_2_3.First().y + 2);
            //--------------Rotate on 90 -2-----------------------------------
            var pat685_2_3_4 = from Dot d in get_non_blocked
                               where d.Own == Owner
                                   & this[d.x + 3, d.y - 2].Own == Owner & this[d.x + 3, d.y - 2].Blocked == false
                                   & this[d.x + 2, d.y - 1].Own == Owner & this[d.x + 2, d.y - 1].Blocked == false
                                   & this[d.x + 3, d.y].Own == Owner & this[d.x + 3, d.y].Blocked == false
                                   & this[d.x + 2, d.y + 1].Own == Owner & this[d.x + 2, d.y + 1].Blocked == false
                                   & this[d.x + 1, d.y + 1].Own == Owner & this[d.x + 1, d.y + 1].Blocked == false
                                   & this[d.x, d.y - 1].Own == 0 & this[d.x, d.y - 1].Blocked == false
                                   & this[d.x, d.y - 2].Own == 0 & this[d.x, d.y - 2].Blocked == false
                                   & this[d.x + 1, d.y - 2].Own == 0 & this[d.x + 1, d.y - 2].Blocked == false
                                   & this[d.x + 2, d.y - 3].Own == 0 & this[d.x + 2, d.y - 3].Blocked == false
                                   & this[d.x + 1, d.y - 3].Own == 0 & this[d.x + 1, d.y - 3].Blocked == false
                                   & this[d.x + 2, d.y - 2].Own == enemy_own & this[d.x + 2, d.y - 2].Blocked == false
                                   & this[d.x + 1, d.y - 1].Own == enemy_own & this[d.x + 1, d.y - 1].Blocked == false
                                   & this[d.x - 1, d.y - 1].Own == 0 & this[d.x - 1, d.y - 1].Blocked == false
                               select d;
            if (pat685_2_3_4.Count() > 0) return new Dot(pat685_2_3_4.First().x + 1, pat685_2_3_4.First().y - 2);
            //============================================================================================================== 
            iNumberPattern = 632;
            var pat632 = from Dot d in get_non_blocked
                         where d.Own == Owner
                             & this[d.x, d.y - 1].Own == enemy_own & this[d.x, d.y - 1].Blocked == false
                             & this[d.x - 1, d.y].Own == enemy_own & this[d.x - 1, d.y].Blocked == false
                             & this[d.x - 2, d.y + 1].Own == enemy_own & this[d.x - 2, d.y + 1].Blocked == false
                             & this[d.x - 1, d.y + 1].Own != enemy_own & this[d.x - 1, d.y + 1].Blocked == false
                             & this[d.x - 2, d.y + 2].Own == enemy_own & this[d.x - 2, d.y + 2].Blocked == false
                             & this[d.x - 1, d.y + 3].Own == enemy_own & this[d.x - 1, d.y + 3].Blocked == false
                             & this[d.x, d.y + 2].Own == 0 & this[d.x, d.y + 2].Blocked == false
                             & this[d.x, d.y + 1].Own == 0 & this[d.x, d.y + 1].Blocked == false
                             & this[d.x + 1, d.y].Own == 0 & this[d.x + 1, d.y].Blocked == false
                             & this[d.x + 1, d.y + 1].Own == 0 & this[d.x + 1, d.y + 1].Blocked == false
                             & this[d.x - 1, d.y + 2].Own != enemy_own & this[d.x - 1, d.y + 2].Blocked == false
                         select d;
            if (pat632.Count() > 0) return new Dot(pat632.First().x, pat632.First().y + 1);
            //180 Rotate=========================================================================================================== 
            var pat632_2 = from Dot d in get_non_blocked
                           where d.Own == Owner
                               & this[d.x, d.y + 1].Own == enemy_own & this[d.x, d.y + 1].Blocked == false
                               & this[d.x + 1, d.y].Own == enemy_own & this[d.x + 1, d.y].Blocked == false
                               & this[d.x + 2, d.y - 1].Own == enemy_own & this[d.x + 2, d.y - 1].Blocked == false
                               & this[d.x + 1, d.y - 1].Own != enemy_own & this[d.x + 1, d.y - 1].Blocked == false
                               & this[d.x + 2, d.y - 2].Own == enemy_own & this[d.x + 2, d.y - 2].Blocked == false
                               & this[d.x + 1, d.y - 3].Own == enemy_own & this[d.x + 1, d.y - 3].Blocked == false
                               & this[d.x, d.y - 2].Own == 0 & this[d.x, d.y - 2].Blocked == false
                               & this[d.x, d.y - 1].Own == 0 & this[d.x, d.y - 1].Blocked == false
                               & this[d.x - 1, d.y].Own == 0 & this[d.x - 1, d.y].Blocked == false
                               & this[d.x - 1, d.y - 1].Own == 0 & this[d.x - 1, d.y - 1].Blocked == false
                               & this[d.x + 1, d.y - 2].Own != enemy_own & this[d.x + 1, d.y - 2].Blocked == false
                           select d;
            if (pat632_2.Count() > 0) return new Dot(pat632_2.First().x, pat632_2.First().y - 1);
            //--------------Rotate on 90-----------------------------------
            var pat632_2_3 = from Dot d in get_non_blocked
                             where d.Own == Owner
                                 & this[d.x + 1, d.y].Own == enemy_own & this[d.x + 1, d.y].Blocked == false
                                 & this[d.x, d.y + 1].Own == enemy_own & this[d.x, d.y + 1].Blocked == false
                                 & this[d.x - 1, d.y + 2].Own == enemy_own & this[d.x - 1, d.y + 2].Blocked == false
                                 & this[d.x - 1, d.y + 1].Own != enemy_own & this[d.x - 1, d.y + 1].Blocked == false
                                 & this[d.x - 2, d.y + 2].Own == enemy_own & this[d.x - 2, d.y + 2].Blocked == false
                                 & this[d.x - 3, d.y + 1].Own == enemy_own & this[d.x - 3, d.y + 1].Blocked == false
                                 & this[d.x - 2, d.y].Own == 0 & this[d.x - 2, d.y].Blocked == false
                                 & this[d.x - 1, d.y].Own == 0 & this[d.x - 1, d.y].Blocked == false
                                 & this[d.x, d.y - 1].Own == 0 & this[d.x, d.y - 1].Blocked == false
                                 & this[d.x - 1, d.y - 1].Own == 0 & this[d.x - 1, d.y - 1].Blocked == false
                                 & this[d.x - 2, d.y + 1].Own != enemy_own & this[d.x - 2, d.y + 1].Blocked == false
                             select d;
            if (pat632_2_3.Count() > 0) return new Dot(pat632_2_3.First().x - 1, pat632_2_3.First().y);
            //--------------Rotate on 90 -2-----------------------------------
            var pat632_2_3_4 = from Dot d in get_non_blocked
                               where d.Own == Owner
                                   & this[d.x - 1, d.y].Own == enemy_own & this[d.x - 1, d.y].Blocked == false
                                   & this[d.x, d.y - 1].Own == enemy_own & this[d.x, d.y - 1].Blocked == false
                                   & this[d.x + 1, d.y - 2].Own == enemy_own & this[d.x + 1, d.y - 2].Blocked == false
                                   & this[d.x + 1, d.y - 1].Own != enemy_own & this[d.x + 1, d.y - 1].Blocked == false
                                   & this[d.x + 2, d.y - 2].Own == enemy_own & this[d.x + 2, d.y - 2].Blocked == false
                                   & this[d.x + 3, d.y - 1].Own == enemy_own & this[d.x + 3, d.y - 1].Blocked == false
                                   & this[d.x + 2, d.y].Own == 0 & this[d.x + 2, d.y].Blocked == false
                                   & this[d.x + 1, d.y].Own == 0 & this[d.x + 1, d.y].Blocked == false
                                   & this[d.x, d.y + 1].Own == 0 & this[d.x, d.y + 1].Blocked == false
                                   & this[d.x + 1, d.y + 1].Own == 0 & this[d.x + 1, d.y + 1].Blocked == false
                                   & this[d.x + 2, d.y - 1].Own != enemy_own & this[d.x + 2, d.y - 1].Blocked == false
                               select d;
            if (pat632_2_3_4.Count() > 0) return new Dot(pat632_2_3_4.First().x + 1, pat632_2_3_4.First().y);
            //============================================================================================================== 
            iNumberPattern = 1938;
            var pat1938 = from Dot d in get_non_blocked
                          where d.Own == Owner
                              & this[d.x, d.y - 3].Own == Owner & this[d.x, d.y - 3].Blocked == false
                              & this[d.x - 1, d.y - 2].Own == Owner & this[d.x - 1, d.y - 2].Blocked == false
                              & this[d.x - 1, d.y - 1].Own == Owner & this[d.x - 1, d.y - 1].Blocked == false
                              & this[d.x + 1, d.y + 1].Own == Owner & this[d.x + 1, d.y + 1].Blocked == false
                              & this[d.x + 1, d.y].Own == enemy_own & this[d.x + 1, d.y].Blocked == false
                              & this[d.x, d.y - 2].Own != Owner & this[d.x, d.y - 2].Blocked == false
                              & this[d.x, d.y - 1].Own != Owner & this[d.x, d.y - 1].Blocked == false
                              & this[d.x + 1, d.y - 2].Own == 0 & this[d.x + 1, d.y - 2].Blocked == false
                              & this[d.x + 1, d.y - 1].Own == 0 & this[d.x + 1, d.y - 1].Blocked == false
                              & this[d.x + 2, d.y].Own == 0 & this[d.x + 2, d.y].Blocked == false
                              & this[d.x + 2, d.y + 1].Own == 0 & this[d.x + 2, d.y + 1].Blocked == false
                              & this[d.x + 1, d.y - 3].Own == 0 & this[d.x + 1, d.y - 3].Blocked == false
                              & this[d.x + 2, d.y - 1].Own == 0 & this[d.x + 2, d.y - 1].Blocked == false
                          select d;
            if (pat1938.Count() > 0) return new Dot(pat1938.First().x + 1, pat1938.First().y - 1);
            //180 Rotate=========================================================================================================== 
            var pat1938_2 = from Dot d in get_non_blocked
                            where d.Own == Owner
                                & this[d.x, d.y + 3].Own == Owner & this[d.x, d.y + 3].Blocked == false
                                & this[d.x + 1, d.y + 2].Own == Owner & this[d.x + 1, d.y + 2].Blocked == false
                                & this[d.x + 1, d.y + 1].Own == Owner & this[d.x + 1, d.y + 1].Blocked == false
                                & this[d.x - 1, d.y - 1].Own == Owner & this[d.x - 1, d.y - 1].Blocked == false
                                & this[d.x - 1, d.y].Own == enemy_own & this[d.x - 1, d.y].Blocked == false
                                & this[d.x, d.y + 2].Own != Owner & this[d.x, d.y + 2].Blocked == false
                                & this[d.x, d.y + 1].Own != Owner & this[d.x, d.y + 1].Blocked == false
                                & this[d.x - 1, d.y + 2].Own == 0 & this[d.x - 1, d.y + 2].Blocked == false
                                & this[d.x - 1, d.y + 1].Own == 0 & this[d.x - 1, d.y + 1].Blocked == false
                                & this[d.x - 2, d.y].Own == 0 & this[d.x - 2, d.y].Blocked == false
                                & this[d.x - 2, d.y - 1].Own == 0 & this[d.x - 2, d.y - 1].Blocked == false
                                & this[d.x - 1, d.y + 3].Own == 0 & this[d.x - 1, d.y + 3].Blocked == false
                                & this[d.x - 2, d.y + 1].Own == 0 & this[d.x - 2, d.y + 1].Blocked == false
                            select d;
            if (pat1938_2.Count() > 0) return new Dot(pat1938_2.First().x - 1, pat1938_2.First().y + 1);
            //--------------Rotate on 90-----------------------------------
            var pat1938_2_3 = from Dot d in get_non_blocked
                              where d.Own == Owner
                                  & this[d.x + 3, d.y].Own == Owner & this[d.x + 3, d.y].Blocked == false
                                  & this[d.x + 2, d.y + 1].Own == Owner & this[d.x + 2, d.y + 1].Blocked == false
                                  & this[d.x + 1, d.y + 1].Own == Owner & this[d.x + 1, d.y + 1].Blocked == false
                                  & this[d.x - 1, d.y - 1].Own == Owner & this[d.x - 1, d.y - 1].Blocked == false
                                  & this[d.x, d.y - 1].Own == enemy_own & this[d.x, d.y - 1].Blocked == false
                                  & this[d.x + 2, d.y].Own != Owner & this[d.x + 2, d.y].Blocked == false
                                  & this[d.x + 1, d.y].Own != Owner & this[d.x + 1, d.y].Blocked == false
                                  & this[d.x + 2, d.y - 1].Own == 0 & this[d.x + 2, d.y - 1].Blocked == false
                                  & this[d.x + 1, d.y - 1].Own == 0 & this[d.x + 1, d.y - 1].Blocked == false
                                  & this[d.x, d.y - 2].Own == 0 & this[d.x, d.y - 2].Blocked == false
                                  & this[d.x - 1, d.y - 2].Own == 0 & this[d.x - 1, d.y - 2].Blocked == false
                                  & this[d.x + 3, d.y - 1].Own == 0 & this[d.x + 3, d.y - 1].Blocked == false
                                  & this[d.x + 1, d.y - 2].Own == 0 & this[d.x + 1, d.y - 2].Blocked == false
                              select d;
            if (pat1938_2_3.Count() > 0) return new Dot(pat1938_2_3.First().x + 1, pat1938_2_3.First().y - 1);
            //--------------Rotate on 90 -2-----------------------------------
            var pat1938_2_3_4 = from Dot d in get_non_blocked
                                where d.Own == Owner
                                    & this[d.x - 3, d.y].Own == Owner & this[d.x - 3, d.y].Blocked == false
                                    & this[d.x - 2, d.y - 1].Own == Owner & this[d.x - 2, d.y - 1].Blocked == false
                                    & this[d.x - 1, d.y - 1].Own == Owner & this[d.x - 1, d.y - 1].Blocked == false
                                    & this[d.x + 1, d.y + 1].Own == Owner & this[d.x + 1, d.y + 1].Blocked == false
                                    & this[d.x, d.y + 1].Own == enemy_own & this[d.x, d.y + 1].Blocked == false
                                    & this[d.x - 2, d.y].Own != Owner & this[d.x - 2, d.y].Blocked == false
                                    & this[d.x - 1, d.y].Own != Owner & this[d.x - 1, d.y].Blocked == false
                                    & this[d.x - 2, d.y + 1].Own == 0 & this[d.x - 2, d.y + 1].Blocked == false
                                    & this[d.x - 1, d.y + 1].Own == 0 & this[d.x - 1, d.y + 1].Blocked == false
                                    & this[d.x, d.y + 2].Own == 0 & this[d.x, d.y + 2].Blocked == false
                                    & this[d.x + 1, d.y + 2].Own == 0 & this[d.x + 1, d.y + 2].Blocked == false
                                    & this[d.x - 3, d.y + 1].Own == 0 & this[d.x - 3, d.y + 1].Blocked == false
                                    & this[d.x - 1, d.y + 2].Own == 0 & this[d.x - 1, d.y + 2].Blocked == false
                                select d;
            if (pat1938_2_3_4.Count() > 0) return new Dot(pat1938_2_3_4.First().x - 1, pat1938_2_3_4.First().y + 1);
            //============================================================================================================== 
            iNumberPattern = 1775;
            var pat1775 = from Dot d in get_non_blocked
                          where d.Own == Owner
                              & this[d.x + 1, d.y + 1].Own == Owner & this[d.x + 1, d.y + 1].Blocked == false
                              & this[d.x + 1, d.y].Own == enemy_own & this[d.x + 1, d.y].Blocked == false
                              & this[d.x + 2, d.y].Own == 0 & this[d.x + 2, d.y].Blocked == false
                              & this[d.x + 1, d.y - 1].Own == 0 & this[d.x + 1, d.y - 1].Blocked == false
                              & this[d.x + 2, d.y - 1].Own == 0 & this[d.x + 2, d.y - 1].Blocked == false
                              & this[d.x + 1, d.y - 2].Own == 0 & this[d.x + 1, d.y - 2].Blocked == false
                              & this[d.x, d.y - 2].Own == Owner & this[d.x, d.y - 2].Blocked == false
                              & this[d.x - 1, d.y - 2].Own == Owner & this[d.x - 1, d.y - 2].Blocked == false
                              & this[d.x - 2, d.y - 1].Own == Owner & this[d.x - 2, d.y - 1].Blocked == false
                              & this[d.x - 1, d.y].Own == 0 & this[d.x - 1, d.y].Blocked == false
                              & this[d.x, d.y - 1].Own == enemy_own & this[d.x, d.y - 1].Blocked == false
                          select d;
            if (pat1775.Count() > 0) return new Dot(pat1775.First().x + 1, pat1775.First().y - 1);
            //180 Rotate=========================================================================================================== 
            var pat1775_2 = from Dot d in get_non_blocked
                            where d.Own == Owner
                                & this[d.x - 1, d.y - 1].Own == Owner & this[d.x - 1, d.y - 1].Blocked == false
                                & this[d.x - 1, d.y].Own == enemy_own & this[d.x - 1, d.y].Blocked == false
                                & this[d.x - 2, d.y].Own == 0 & this[d.x - 2, d.y].Blocked == false
                                & this[d.x - 1, d.y + 1].Own == 0 & this[d.x - 1, d.y + 1].Blocked == false
                                & this[d.x - 2, d.y + 1].Own == 0 & this[d.x - 2, d.y + 1].Blocked == false
                                & this[d.x - 1, d.y + 2].Own == 0 & this[d.x - 1, d.y + 2].Blocked == false
                                & this[d.x, d.y + 2].Own == Owner & this[d.x, d.y + 2].Blocked == false
                                & this[d.x + 1, d.y + 2].Own == Owner & this[d.x + 1, d.y + 2].Blocked == false
                                & this[d.x + 2, d.y + 1].Own == Owner & this[d.x + 2, d.y + 1].Blocked == false
                                & this[d.x + 1, d.y].Own == 0 & this[d.x + 1, d.y].Blocked == false
                                & this[d.x, d.y + 1].Own == enemy_own & this[d.x, d.y + 1].Blocked == false
                            select d;
            if (pat1775_2.Count() > 0) return new Dot(pat1775_2.First().x - 1, pat1775_2.First().y + 1);
            //--------------Rotate on 90-----------------------------------
            var pat1775_2_3 = from Dot d in get_non_blocked
                              where d.Own == Owner
                                  & this[d.x - 1, d.y - 1].Own == Owner & this[d.x - 1, d.y - 1].Blocked == false
                                  & this[d.x, d.y - 1].Own == enemy_own & this[d.x, d.y - 1].Blocked == false
                                  & this[d.x, d.y - 2].Own == 0 & this[d.x, d.y - 2].Blocked == false
                                  & this[d.x + 1, d.y - 1].Own == 0 & this[d.x + 1, d.y - 1].Blocked == false
                                  & this[d.x + 1, d.y - 2].Own == 0 & this[d.x + 1, d.y - 2].Blocked == false
                                  & this[d.x + 2, d.y - 1].Own == 0 & this[d.x + 2, d.y - 1].Blocked == false
                                  & this[d.x + 2, d.y].Own == Owner & this[d.x + 2, d.y].Blocked == false
                                  & this[d.x + 2, d.y + 1].Own == Owner & this[d.x + 2, d.y + 1].Blocked == false
                                  & this[d.x + 1, d.y + 2].Own == Owner & this[d.x + 1, d.y + 2].Blocked == false
                                  & this[d.x, d.y + 1].Own == 0 & this[d.x, d.y + 1].Blocked == false
                                  & this[d.x + 1, d.y].Own == enemy_own & this[d.x + 1, d.y].Blocked == false
                              select d;
            if (pat1775_2_3.Count() > 0) return new Dot(pat1775_2_3.First().x + 1, pat1775_2_3.First().y - 1);
            //--------------Rotate on 90 -2-----------------------------------
            var pat1775_2_3_4 = from Dot d in get_non_blocked
                                where d.Own == Owner
                                    & this[d.x + 1, d.y + 1].Own == Owner & this[d.x + 1, d.y + 1].Blocked == false
                                    & this[d.x, d.y + 1].Own == enemy_own & this[d.x, d.y + 1].Blocked == false
                                    & this[d.x, d.y + 2].Own == 0 & this[d.x, d.y + 2].Blocked == false
                                    & this[d.x - 1, d.y + 1].Own == 0 & this[d.x - 1, d.y + 1].Blocked == false
                                    & this[d.x - 1, d.y + 2].Own == 0 & this[d.x - 1, d.y + 2].Blocked == false
                                    & this[d.x - 2, d.y + 1].Own == 0 & this[d.x - 2, d.y + 1].Blocked == false
                                    & this[d.x - 2, d.y].Own == Owner & this[d.x - 2, d.y].Blocked == false
                                    & this[d.x - 2, d.y - 1].Own == Owner & this[d.x - 2, d.y - 1].Blocked == false
                                    & this[d.x - 1, d.y - 2].Own == Owner & this[d.x - 1, d.y - 2].Blocked == false
                                    & this[d.x, d.y - 1].Own == 0 & this[d.x, d.y - 1].Blocked == false
                                    & this[d.x - 1, d.y].Own == enemy_own & this[d.x - 1, d.y].Blocked == false
                                select d;
            if (pat1775_2_3_4.Count() > 0) return new Dot(pat1775_2_3_4.First().x - 1, pat1775_2_3_4.First().y + 1);
            //============================================================================================================== 
            iNumberPattern = 274;
            var pat274 = from Dot d in get_non_blocked
                         where d.Own == Owner
                             & this[d.x - 1, d.y].Own == enemy_own & this[d.x - 1, d.y].Blocked == false
                             & this[d.x, d.y - 1].Own == enemy_own & this[d.x, d.y - 1].Blocked == false
                             & this[d.x, d.y - 2].Own == enemy_own & this[d.x, d.y - 2].Blocked == false
                             & this[d.x, d.y - 3].Own == enemy_own & this[d.x, d.y - 3].Blocked == false
                             & this[d.x + 1, d.y - 4].Own == enemy_own & this[d.x + 1, d.y - 4].Blocked == false
                             & this[d.x + 2, d.y - 3].Own == enemy_own & this[d.x + 2, d.y - 3].Blocked == false
                             & this[d.x + 2, d.y - 2].Own == enemy_own & this[d.x + 2, d.y - 2].Blocked == false
                             & this[d.x + 2, d.y - 1].Own == 0 & this[d.x + 2, d.y - 1].Blocked == false
                             & this[d.x + 2, d.y].Own == 0 & this[d.x + 2, d.y].Blocked == false
                             & this[d.x + 1, d.y].Own == 0 & this[d.x + 1, d.y].Blocked == false
                             & this[d.x + 1, d.y + 1].Own == 0 & this[d.x + 1, d.y + 1].Blocked == false
                             & this[d.x, d.y + 1].Own == 0 & this[d.x, d.y + 1].Blocked == false
                             & this[d.x + 1, d.y - 1].Own != enemy_own & this[d.x + 1, d.y - 1].Blocked == false
                             & this[d.x + 1, d.y - 2].Own != enemy_own & this[d.x + 1, d.y - 2].Blocked == false
                             & this[d.x + 1, d.y - 3].Own != enemy_own & this[d.x + 1, d.y - 3].Blocked == false
                         select d;
            if (pat274.Count() > 0) return new Dot(pat274.First().x + 1, pat274.First().y);
            //180 Rotate=========================================================================================================== 
            var pat274_2 = from Dot d in get_non_blocked
                           where d.Own == Owner
                               & this[d.x + 1, d.y].Own == enemy_own & this[d.x + 1, d.y].Blocked == false
                               & this[d.x, d.y + 1].Own == enemy_own & this[d.x, d.y + 1].Blocked == false
                               & this[d.x, d.y + 2].Own == enemy_own & this[d.x, d.y + 2].Blocked == false
                               & this[d.x, d.y + 3].Own == enemy_own & this[d.x, d.y + 3].Blocked == false
                               & this[d.x - 1, d.y + 4].Own == enemy_own & this[d.x - 1, d.y + 4].Blocked == false
                               & this[d.x - 2, d.y + 3].Own == enemy_own & this[d.x - 2, d.y + 3].Blocked == false
                               & this[d.x - 2, d.y + 2].Own == enemy_own & this[d.x - 2, d.y + 2].Blocked == false
                               & this[d.x - 2, d.y + 1].Own == 0 & this[d.x - 2, d.y + 1].Blocked == false
                               & this[d.x - 2, d.y].Own == 0 & this[d.x - 2, d.y].Blocked == false
                               & this[d.x - 1, d.y].Own == 0 & this[d.x - 1, d.y].Blocked == false
                               & this[d.x - 1, d.y - 1].Own == 0 & this[d.x - 1, d.y - 1].Blocked == false
                               & this[d.x, d.y - 1].Own == 0 & this[d.x, d.y - 1].Blocked == false
                               & this[d.x - 1, d.y + 1].Own != enemy_own & this[d.x - 1, d.y + 1].Blocked == false
                               & this[d.x - 1, d.y + 2].Own != enemy_own & this[d.x - 1, d.y + 2].Blocked == false
                               & this[d.x - 1, d.y + 3].Own != enemy_own & this[d.x - 1, d.y + 3].Blocked == false
                           select d;
            if (pat274_2.Count() > 0) return new Dot(pat274_2.First().x - 1, pat274_2.First().y);
            //--------------Rotate on 90-----------------------------------
            var pat274_2_3 = from Dot d in get_non_blocked
                             where d.Own == Owner
                                 & this[d.x, d.y + 1].Own == enemy_own & this[d.x, d.y + 1].Blocked == false
                                 & this[d.x + 1, d.y].Own == enemy_own & this[d.x + 1, d.y].Blocked == false
                                 & this[d.x + 2, d.y].Own == enemy_own & this[d.x + 2, d.y].Blocked == false
                                 & this[d.x + 3, d.y].Own == enemy_own & this[d.x + 3, d.y].Blocked == false
                                 & this[d.x + 4, d.y - 1].Own == enemy_own & this[d.x + 4, d.y - 1].Blocked == false
                                 & this[d.x + 3, d.y - 2].Own == enemy_own & this[d.x + 3, d.y - 2].Blocked == false
                                 & this[d.x + 2, d.y - 2].Own == enemy_own & this[d.x + 2, d.y - 2].Blocked == false
                                 & this[d.x + 1, d.y - 2].Own == 0 & this[d.x + 1, d.y - 2].Blocked == false
                                 & this[d.x, d.y - 2].Own == 0 & this[d.x, d.y - 2].Blocked == false
                                 & this[d.x, d.y - 1].Own == 0 & this[d.x, d.y - 1].Blocked == false
                                 & this[d.x - 1, d.y - 1].Own == 0 & this[d.x - 1, d.y - 1].Blocked == false
                                 & this[d.x - 1, d.y].Own == 0 & this[d.x - 1, d.y].Blocked == false
                                 & this[d.x + 1, d.y - 1].Own != enemy_own & this[d.x + 1, d.y - 1].Blocked == false
                                 & this[d.x + 2, d.y - 1].Own != enemy_own & this[d.x + 2, d.y - 1].Blocked == false
                                 & this[d.x + 3, d.y - 1].Own != enemy_own & this[d.x + 3, d.y - 1].Blocked == false
                             select d;
            if (pat274_2_3.Count() > 0) return new Dot(pat274_2_3.First().x, pat274_2_3.First().y - 1);
            //--------------Rotate on 90 -2-----------------------------------
            var pat274_2_3_4 = from Dot d in get_non_blocked
                               where d.Own == Owner
                                   & this[d.x, d.y - 1].Own == enemy_own & this[d.x, d.y - 1].Blocked == false
                                   & this[d.x - 1, d.y].Own == enemy_own & this[d.x - 1, d.y].Blocked == false
                                   & this[d.x - 2, d.y].Own == enemy_own & this[d.x - 2, d.y].Blocked == false
                                   & this[d.x - 3, d.y].Own == enemy_own & this[d.x - 3, d.y].Blocked == false
                                   & this[d.x - 4, d.y + 1].Own == enemy_own & this[d.x - 4, d.y + 1].Blocked == false
                                   & this[d.x - 3, d.y + 2].Own == enemy_own & this[d.x - 3, d.y + 2].Blocked == false
                                   & this[d.x - 2, d.y + 2].Own == enemy_own & this[d.x - 2, d.y + 2].Blocked == false
                                   & this[d.x - 1, d.y + 2].Own == 0 & this[d.x - 1, d.y + 2].Blocked == false
                                   & this[d.x, d.y + 2].Own == 0 & this[d.x, d.y + 2].Blocked == false
                                   & this[d.x, d.y + 1].Own == 0 & this[d.x, d.y + 1].Blocked == false
                                   & this[d.x + 1, d.y + 1].Own == 0 & this[d.x + 1, d.y + 1].Blocked == false
                                   & this[d.x + 1, d.y].Own == 0 & this[d.x + 1, d.y].Blocked == false
                                   & this[d.x - 1, d.y + 1].Own != enemy_own & this[d.x - 1, d.y + 1].Blocked == false
                                   & this[d.x - 2, d.y + 1].Own != enemy_own & this[d.x - 2, d.y + 1].Blocked == false
                                   & this[d.x - 3, d.y + 1].Own != enemy_own & this[d.x - 3, d.y + 1].Blocked == false
                               select d;
            if (pat274_2_3_4.Count() > 0) return new Dot(pat274_2_3_4.First().x, pat274_2_3_4.First().y + 1);
            //============================================================================================================== 
            iNumberPattern = 44;
            var pat44 = from Dot d in get_non_blocked
                        where d.Own == Owner
                            & this[d.x - 1, d.y + 2].Own == Owner & this[d.x - 1, d.y + 2].Blocked == false
                            & this[d.x, d.y + 1].Own == enemy_own & this[d.x, d.y + 1].Blocked == false
                            & this[d.x + 1, d.y].Own == enemy_own & this[d.x + 1, d.y].Blocked == false
                            & this[d.x + 2, d.y - 1].Own == Owner & this[d.x + 2, d.y - 1].Blocked == false
                            & this[d.x, d.y + 2].Own == 0 & this[d.x, d.y + 2].Blocked == false
                            & this[d.x + 1, d.y + 1].Own == 0 & this[d.x + 1, d.y + 1].Blocked == false
                            & this[d.x + 2, d.y].Own == 0 & this[d.x + 2, d.y].Blocked == false
                            & this[d.x + 2, d.y + 1].Own == 0 & this[d.x + 2, d.y + 1].Blocked == false
                            & this[d.x + 1, d.y + 2].Own == 0 & this[d.x + 1, d.y + 2].Blocked == false
                        select d;
            if (pat44.Count() > 0) return new Dot(pat44.First().x + 1, pat44.First().y + 1);
            //180 Rotate=========================================================================================================== 
            var pat44_2 = from Dot d in get_non_blocked
                          where d.Own == Owner
                              & this[d.x + 1, d.y - 2].Own == Owner & this[d.x + 1, d.y - 2].Blocked == false
                              & this[d.x, d.y - 1].Own == enemy_own & this[d.x, d.y - 1].Blocked == false
                              & this[d.x - 1, d.y].Own == enemy_own & this[d.x - 1, d.y].Blocked == false
                              & this[d.x - 2, d.y + 1].Own == Owner & this[d.x - 2, d.y + 1].Blocked == false
                              & this[d.x, d.y - 2].Own == 0 & this[d.x, d.y - 2].Blocked == false
                              & this[d.x - 1, d.y - 1].Own == 0 & this[d.x - 1, d.y - 1].Blocked == false
                              & this[d.x - 2, d.y].Own == 0 & this[d.x - 2, d.y].Blocked == false
                              & this[d.x - 2, d.y - 1].Own == 0 & this[d.x - 2, d.y - 1].Blocked == false
                              & this[d.x - 1, d.y - 2].Own == 0 & this[d.x - 1, d.y - 2].Blocked == false
                          select d;
            if (pat44_2.Count() > 0) return new Dot(pat44_2.First().x - 1, pat44_2.First().y - 1);
            //--------------Rotate on 90-----------------------------------
            var pat44_2_3 = from Dot d in get_non_blocked
                            where d.Own == Owner
                                & this[d.x + 2, d.y + 1].Own == Owner & this[d.x + 2, d.y + 1].Blocked == false
                                & this[d.x + 1, d.y].Own == enemy_own & this[d.x + 1, d.y].Blocked == false
                                & this[d.x, d.y - 1].Own == enemy_own & this[d.x, d.y - 1].Blocked == false
                                & this[d.x - 1, d.y - 2].Own == Owner & this[d.x - 1, d.y - 2].Blocked == false
                                & this[d.x + 2, d.y].Own == 0 & this[d.x + 2, d.y].Blocked == false
                                & this[d.x + 1, d.y - 1].Own == 0 & this[d.x + 1, d.y - 1].Blocked == false
                                & this[d.x, d.y - 2].Own == 0 & this[d.x, d.y - 2].Blocked == false
                                & this[d.x + 1, d.y - 2].Own == 0 & this[d.x + 1, d.y - 2].Blocked == false
                                & this[d.x + 2, d.y - 1].Own == 0 & this[d.x + 2, d.y - 1].Blocked == false
                            select d;
            if (pat44_2_3.Count() > 0) return new Dot(pat44_2_3.First().x + 1, pat44_2_3.First().y - 1);
            //--------------Rotate on 90 -2-----------------------------------
            var pat44_2_3_4 = from Dot d in get_non_blocked
                              where d.Own == Owner
                                  & this[d.x - 2, d.y - 1].Own == Owner & this[d.x - 2, d.y - 1].Blocked == false
                                  & this[d.x - 1, d.y].Own == enemy_own & this[d.x - 1, d.y].Blocked == false
                                  & this[d.x, d.y + 1].Own == enemy_own & this[d.x, d.y + 1].Blocked == false
                                  & this[d.x + 1, d.y + 2].Own == Owner & this[d.x + 1, d.y + 2].Blocked == false
                                  & this[d.x - 2, d.y].Own == 0 & this[d.x - 2, d.y].Blocked == false
                                  & this[d.x - 1, d.y + 1].Own == 0 & this[d.x - 1, d.y + 1].Blocked == false
                                  & this[d.x, d.y + 2].Own == 0 & this[d.x, d.y + 2].Blocked == false
                                  & this[d.x - 1, d.y + 2].Own == 0 & this[d.x - 1, d.y + 2].Blocked == false
                                  & this[d.x - 2, d.y + 1].Own == 0 & this[d.x - 2, d.y + 1].Blocked == false
                              select d;
            if (pat44_2_3_4.Count() > 0) return new Dot(pat44_2_3_4.First().x - 1, pat44_2_3_4.First().y + 1);
            //============================================================================================================== 



            //=*=*=*=*=*=*=*=*=*=*=*=*=*=*=*=*=*=*=*=*=*=*=*=*=*=*=*=*=*=*=*=*=*=*=*=*=*
            return null;//если никаких паттернов не найдено возвращаем нуль

        }
        private Dot CheckPatternMove1(int Owner)//паттерны без вражеской точки
        {
            iNumberPattern = 1;
            var pat1 = from Dot d in this
                       where d.Own == 0
                           && this[d.x - 1, d.y + 1].Own == Owner && this[d.x - 1, d.y + 1].Blocked == false
                           && this[d.x + 1, d.y - 1].Own == Owner && this[d.x + 1, d.y - 1].Blocked == false
                           && this[d.x - 1, d.y].Own == 0 && this[d.x - 1, d.y].Blocked == false
                           && this[d.x - 1, d.y - 1].Own == 0 && this[d.x - 1, d.y - 1].Blocked == false
                           && this[d.x, d.y - 1].Own == 0 && this[d.x, d.y - 1].Blocked == false
                           && this[d.x + 1, d.y].Own == 0 && this[d.x + 1, d.y].Blocked == false
                           && this[d.x + 1, d.y + 1].Own == 0 && this[d.x + 1, d.y + 1].Blocked == false
                           && this[d.x, d.y + 1].Own == 0 && this[d.x, d.y + 1].Blocked == false
                           && this[d.x, d.y - 2].Own == 0 && this[d.x, d.y - 2].Blocked == false
                           && this[d.x + 2, d.y].Own == 0 && this[d.x + 2, d.y].Blocked == false
                           && this[d.x, d.y + 2].Own == 0 && this[d.x, d.y + 2].Blocked == false
                           && this[d.x - 2, d.y].Own == 0 && this[d.x - 2, d.y].Blocked == false
                       select d;
            if (pat1.Count() > 0) return new Dot(pat1.First().x, pat1.First().y);
            //--------------Rotate on 90-----------------------------------
            var pat1_2_3_4 = from Dot d in this
                             where d.Own == 0
                                 && this[d.x + 1, d.y + 1].Own == Owner && this[d.x + 1, d.y - 1].Blocked == false
                                 && this[d.x - 1, d.y - 1].Own == Owner && this[d.x - 1, d.y + 1].Blocked == false
                                 && this[d.x, d.y + 1].Own == 0 && this[d.x, d.y - 1].Blocked == false
                                 && this[d.x - 1, d.y + 1].Own == 0 && this[d.x - 1, d.y - 1].Blocked == false
                                 && this[d.x - 1, d.y].Own == 0 && this[d.x - 1, d.y].Blocked == false
                                 && this[d.x, d.y - 1].Own == 0 && this[d.x, d.y + 1].Blocked == false
                                 && this[d.x + 1, d.y - 1].Own == 0 && this[d.x + 1, d.y + 1].Blocked == false
                                 && this[d.x + 1, d.y].Own == 0 && this[d.x + 1, d.y].Blocked == false
                                 && this[d.x - 2, d.y].Own == 0 && this[d.x - 2, d.y].Blocked == false
                                 && this[d.x, d.y - 2].Own == 0 && this[d.x, d.y + 2].Blocked == false
                                 && this[d.x + 2, d.y].Own == 0 && this[d.x + 2, d.y].Blocked == false
                                 && this[d.x, d.y + 2].Own == 0 && this[d.x, d.y - 2].Blocked == false
                             select d;
            if (pat1_2_3_4.Count() > 0) return new Dot(pat1_2_3_4.First().x, pat1_2_3_4.First().y);
            //============================================================================================================== 
            iNumberPattern = 883;
            var pat883 = from Dot d in this
                         where d.Own == Owner
                             && this[d.x + 1, d.y].Own == 0 && this[d.x + 1, d.y].Blocked == false
                             && this[d.x + 2, d.y].Own == 0 && this[d.x + 2, d.y].Blocked == false
                             && this[d.x + 3, d.y].Own == 0 && this[d.x + 3, d.y].Blocked == false
                             && this[d.x + 3, d.y - 1].Own == Owner && this[d.x + 3, d.y - 1].Blocked == false
                             && this[d.x + 2, d.y - 1].Own == 0 && this[d.x + 2, d.y - 1].Blocked == false
                             && this[d.x + 1, d.y - 1].Own == 0 && this[d.x + 1, d.y - 1].Blocked == false
                             && this[d.x, d.y - 1].Own == 0 && this[d.x, d.y - 1].Blocked == false
                             && this[d.x + 2, d.y - 2].Own == 0 && this[d.x + 2, d.y - 2].Blocked == false
                             && this[d.x + 1, d.y - 2].Own == 0 && this[d.x + 1, d.y - 2].Blocked == false
                             && this[d.x + 3, d.y - 2].Own == 0 && this[d.x + 3, d.y - 2].Blocked == false
                             && this[d.x, d.y - 2].Own == 0 && this[d.x, d.y - 2].Blocked == false
                         select d;
            if (pat883.Count() > 0) return new Dot(pat883.First().x + 1, pat883.First().y - 1);
            //--------------Rotate on 90-----------------------------------
            var pat883_2_3 = from Dot d in this
                             where d.Own == Owner
                                 && this[d.x, d.y - 1].Own == 0 && this[d.x, d.y - 1].Blocked == false
                                 && this[d.x, d.y - 2].Own == 0 && this[d.x, d.y - 2].Blocked == false
                                 && this[d.x, d.y - 3].Own == 0 && this[d.x, d.y - 3].Blocked == false
                                 && this[d.x + 1, d.y - 3].Own == Owner && this[d.x + 1, d.y - 3].Blocked == false
                                 && this[d.x + 1, d.y - 2].Own == 0 && this[d.x + 1, d.y - 2].Blocked == false
                                 && this[d.x + 1, d.y - 1].Own == 0 && this[d.x + 1, d.y - 1].Blocked == false
                                 && this[d.x + 1, d.y].Own == 0 && this[d.x + 1, d.y].Blocked == false
                                 && this[d.x + 2, d.y - 2].Own == 0 && this[d.x + 2, d.y - 2].Blocked == false
                                 && this[d.x + 2, d.y - 1].Own == 0 && this[d.x + 2, d.y - 1].Blocked == false
                                 && this[d.x + 2, d.y - 3].Own == 0 && this[d.x + 2, d.y - 3].Blocked == false
                                 && this[d.x + 2, d.y].Own == 0 && this[d.x + 2, d.y].Blocked == false
                             select d;
            if (pat883_2_3.Count() > 0) return new Dot(pat883_2_3.First().x + 1, pat883_2_3.First().y - 1);
            //=================================================================================
            // 0d край доски
            // m   *
            iNumberPattern = 2;
            var pat2 = from Dot d in this
                       where d.Own == Owner && d.y == 0 && d.x > 0 && d.x < BoardWidth
                           && this[d.x + 1, d.y + 1].Own == Owner && this[d.x + 1, d.y + 1].Blocked == false
                           && this[d.x, d.y + 1].Own == 0 && this[d.x, d.y + 1].Blocked == false
                       select d;
            if (pat2.Count() > 0) return new Dot(pat2.First().x, pat2.First().y + 1);
            var pat2_2 = from Dot d in this
                         where d.Own == Owner && d.y > 1 && d.y < BoardHeight && d.x == 0
                               && this[d.x + 1, d.y + 1].Own == Owner && this[d.x + 1, d.y + 1].Blocked == false
                               && this[d.x + 1, d.y].Own == 0 && this[d.x + 1, d.y].Blocked == false
                         select d;
            if (pat2_2.Count() > 0) return new Dot(pat2_2.First().x + 1, pat2_2.First().y);
            var pat2_2_3 = from Dot d in this
                           where d.Own == Owner && d.x == BoardWidth - 1 && d.y > 0 && d.y < BoardHeight
                                 && this[d.x - 1, d.y - 1].Own == Owner && this[d.x - 1, d.y - 1].Blocked == false
                                 && this[d.x - 1, d.y].Own == 0 && this[d.x - 1, d.y].Blocked == false
                           select d;
            if (pat2_2_3.Count() > 0) return new Dot(pat2_2_3.First().x - 1, pat2_2_3.First().y);
            var pat2_2_3_4 = from Dot d in this
                             where d.Own == Owner && d.y == BoardHeight - 1 && d.x > 0 && d.x < BoardWidth
                                   && this[d.x - 1, d.y - 1].Own == Owner && this[d.x - 1, d.y - 1].Blocked == false
                                   && this[d.x, d.y - 1].Own == 0 && this[d.x, d.y - 1].Blocked == false
                             select d;
            if (pat2_2_3_4.Count() > 0) return new Dot(pat2_2_3_4.First().x, pat2_2_3_4.First().y - 1);
            iNumberPattern = 4;
            var pat4 = from Dot d in this
                       where d.Own == Owner
                           && this[d.x, d.y - 1].Own == 0 && this[d.x, d.y - 1].Blocked == false
                           && this[d.x + 1, d.y - 2].Own == 0 && this[d.x + 1, d.y - 2].Blocked == false
                           && this[d.x + 2, d.y - 2].Own == Owner && this[d.x + 2, d.y - 2].Blocked == false
                           && this[d.x + 2, d.y - 1].Own == 0 && this[d.x + 2, d.y - 1].Blocked == false
                           && this[d.x + 1, d.y].Own == 0 && this[d.x + 1, d.y].Blocked == false
                           && this[d.x + 1, d.y - 1].Own == 0 && this[d.x + 1, d.y - 1].Blocked == false
                       select d;
            if (pat4.Count() > 0) return new Dot(pat4.First().x + 1, pat4.First().y - 1);
            //180 Rotate=========================================================================================================== 
            //  *
            //     m
            //        d* 
            var pat4_2 = from Dot d in this
                         where d.Own == Owner
                             && this[d.x, d.y - 1].Own == 0 && this[d.x, d.y - 1].Blocked == false
                             && this[d.x - 1, d.y - 2].Own == 0 && this[d.x - 1, d.y - 2].Blocked == false
                             && this[d.x - 2, d.y - 2].Own == Owner && this[d.x - 2, d.y - 2].Blocked == false
                             && this[d.x - 2, d.y - 1].Own == 0 && this[d.x - 2, d.y - 1].Blocked == false
                             && this[d.x - 1, d.y].Own == 0 && this[d.x - 1, d.y].Blocked == false
                             && this[d.x - 1, d.y - 1].Own == 0 && this[d.x - 1, d.y - 1].Blocked == false
                         select d;
            if (pat4_2.Count() > 0) return new Dot(pat4_2.First().x - 1, pat4_2.First().y - 1);
            //============================================================================================================== 
            //d*  m  *
            iNumberPattern = 7;
            var pat7 = from Dot d in this
                       where d.Own == Owner
                           && this[d.x + 1, d.y].Own == 0 && this[d.x + 1, d.y].Blocked == false
                           && this[d.x + 1, d.y - 1].Own == 0 && this[d.x + 1, d.y - 1].Blocked == false
                           && this[d.x + 1, d.y + 1].Own == 0 && this[d.x + 1, d.y + 1].Blocked == false
                           && this[d.x + 2, d.y].Own == Owner && this[d.x + 2, d.y].Blocked == false
                       select d;
            if (pat7.Count() > 0) return new Dot(pat7.First().x + 1, pat7.First().y);
            //--------------Rotate on 90-----------------------------------
            //   *
            //   m
            //   d*
            var pat7_2 = from Dot d in this
                         where d.Own == Owner
                             && this[d.x, d.y - 1].Own == 0 && this[d.x, d.y - 1].Blocked == false
                             && this[d.x - 1, d.y - 1].Own == 0 && this[d.x - 1, d.y - 1].Blocked == false
                             && this[d.x + 1, d.y - 1].Own == 0 && this[d.x + 1, d.y - 1].Blocked == false
                             && this[d.x, d.y - 2].Own == Owner && this[d.x, d.y - 2].Blocked == false
                         select d;
            if (pat7_2.Count() > 0) return new Dot(pat7_2.First().x, pat7_2.First().y - 1);
            //============================================================================================================== 
            //    *
            // m 
            //
            // d*
            iNumberPattern = 8;
            var pat8 = from Dot d in this
                       where d.Own == Owner
                           && this[d.x + 1, d.y - 3].Own == Owner && this[d.x + 1, d.y - 3].Blocked == false
                           && this[d.x, d.y - 2].Own == 0 && this[d.x, d.y - 2].Blocked == false
                           && this[d.x + 1, d.y - 2].Own == 0 && this[d.x + 1, d.y - 2].Blocked == false
                           && this[d.x + 1, d.y - 1].Own == 0 && this[d.x + 1, d.y - 1].Blocked == false
                           && this[d.x, d.y - 1].Own == 0 && this[d.x, d.y - 1].Blocked == false
                           && this[d.x + 1, d.y].Own == 0 && this[d.x + 1, d.y].Blocked == false
                           && this[d.x, d.y - 3].Own == 0 && this[d.x, d.y - 3].Blocked == false
                       select d;
            if (pat8.Count() > 0) return new Dot(pat8.First().x, pat8.First().y - 2);
            //180 Rotate=========================================================================================================== 
            var pat8_2 = from Dot d in this
                         where d.Own == Owner
                             && this[d.x - 1, d.y + 3].Own == Owner && this[d.x - 1, d.y + 3].Blocked == false
                             && this[d.x, d.y + 2].Own == 0 && this[d.x, d.y + 2].Blocked == false
                             && this[d.x - 1, d.y + 2].Own == 0 && this[d.x - 1, d.y + 2].Blocked == false
                             && this[d.x - 1, d.y + 1].Own == 0 && this[d.x - 1, d.y + 1].Blocked == false
                             && this[d.x, d.y + 1].Own == 0 && this[d.x, d.y + 1].Blocked == false
                             && this[d.x - 1, d.y].Own == 0 && this[d.x - 1, d.y].Blocked == false
                             && this[d.x, d.y + 3].Own == 0 && this[d.x, d.y + 3].Blocked == false
                         select d;
            if (pat8_2.Count() > 0) return new Dot(pat8_2.First().x, pat8_2.First().y + 2);
            //--------------Rotate on 90-----------------------------------
            var pat8_2_3 = from Dot d in this
                           where d.Own == Owner
                               && this[d.x + 3, d.y - 1].Own == Owner && this[d.x + 3, d.y - 1].Blocked == false
                               && this[d.x + 2, d.y].Own == 0 && this[d.x + 2, d.y].Blocked == false
                               && this[d.x + 2, d.y - 1].Own == 0 && this[d.x + 2, d.y - 1].Blocked == false
                               && this[d.x + 1, d.y - 1].Own == 0 && this[d.x + 1, d.y - 1].Blocked == false
                               && this[d.x + 1, d.y].Own == 0 && this[d.x + 1, d.y].Blocked == false
                               && this[d.x, d.y - 1].Own == 0 && this[d.x, d.y - 1].Blocked == false
                               && this[d.x + 3, d.y].Own == 0 && this[d.x + 3, d.y].Blocked == false
                           select d;
            if (pat8_2_3.Count() > 0) return new Dot(pat8_2_3.First().x + 2, pat8_2_3.First().y);
            //--------------Rotate on 90 -2-----------------------------------
            var pat8_2_3_4 = from Dot d in this
                             where d.Own == Owner
                                 && this[d.x - 3, d.y + 1].Own == Owner && this[d.x - 3, d.y + 1].Blocked == false
                                 && this[d.x - 2, d.y].Own == 0 && this[d.x - 2, d.y].Blocked == false
                                 && this[d.x - 2, d.y + 1].Own == 0 && this[d.x - 2, d.y + 1].Blocked == false
                                 && this[d.x - 1, d.y + 1].Own == 0 && this[d.x - 1, d.y + 1].Blocked == false
                                 && this[d.x - 1, d.y].Own == 0 && this[d.x - 1, d.y].Blocked == false
                                 && this[d.x, d.y + 1].Own == 0 && this[d.x, d.y + 1].Blocked == false
                                 && this[d.x - 3, d.y].Own == 0 && this[d.x - 3, d.y].Blocked == false
                             select d;
            if (pat8_2_3_4.Count() > 0) return new Dot(pat8_2_3_4.First().x - 2, pat8_2_3_4.First().y);
            //============================================================================================================== 
            //     *
            //        d*  
            //     m
            //============================================================================================================== 
            iNumberPattern = 9;
            var pat9 = from Dot d in this
                       where d.Own == Owner && d.x >= 2
                       && this[d.x - 1, d.y - 1].Own == Owner && this[d.x - 1, d.y - 1].Blocked == false
                       && this[d.x - 1, d.y + 1].Own == 0 && this[d.x - 1, d.y + 1].Blocked == false
                       && this[d.x - 1, d.y].Own == 0 && this[d.x - 1, d.y].Blocked == false
                       && this[d.x - 2, d.y].Own == 0 && this[d.x - 2, d.y].Blocked == false
                       select d;
            if (pat9.Count() > 0) return new Dot(pat9.First().x - 1, pat9.First().y + 1);
            //180 Rotate=========================================================================================================== 
            //     m  
            // d*  
            //     *
            var pat9_2 = from Dot d in this
                         where d.Own == Owner && d.x <= BoardWidth - 2
                               && this[d.x + 1, d.y + 1].Own == Owner && this[d.x + 1, d.y + 1].Blocked == false
                               && this[d.x + 1, d.y - 1].Own == 0 && this[d.x + 1, d.y - 1].Blocked == false
                               && this[d.x + 1, d.y].Own == 0 && this[d.x + 1, d.y].Blocked == false
                               && this[d.x + 2, d.y].Own == 0 && this[d.x + 2, d.y].Blocked == false
                         select d;
            if (pat9_2.Count() > 0) return new Dot(pat9_2.First().x + 1, pat9_2.First().y - 1);
            //--------------Rotate on 90-----------------------------------
            //         
            //     d*
            //  m       *
            var pat9_2_3 = from Dot d in this
                           where d.Own == Owner && d.y <= BoardHeight - 2
                                 && this[d.x + 1, d.y + 1].Own == Owner && this[d.x + 1, d.y + 1].Blocked == false
                                 && this[d.x - 1, d.y + 1].Own == 0 && this[d.x - 1, d.y + 1].Blocked == false
                                 && this[d.x, d.y + 1].Own == 0 && this[d.x, d.y + 1].Blocked == false
                                 && this[d.x, d.y + 2].Own == 0 && this[d.x, d.y + 2].Blocked == false
                           select d;
            if (pat9_2_3.Count() > 0) return new Dot(pat9_2_3.First().x - 1, pat9_2_3.First().y + 1);
            //--------------Rotate on 90 -2-----------------------------------
            // *      m
            //    d*   
            //
            var pat9_2_3_4 = from Dot d in this
                             where d.Own == Owner && d.y >= 2
                                   && this[d.x - 1, d.y - 1].Own == Owner && this[d.x - 1, d.y - 1].Blocked == false
                                   && this[d.x + 1, d.y - 1].Own == 0 && this[d.x + 1, d.y - 1].Blocked == false
                                   && this[d.x, d.y - 1].Own == 0 && this[d.x, d.y - 1].Blocked == false
                                   && this[d.x, d.y - 2].Own == 0 && this[d.x, d.y - 2].Blocked == false
                             select d;
            if (pat9_2_3_4.Count() > 0) return new Dot(pat9_2_3_4.First().x + 1, pat9_2_3_4.First().y - 1);
            //============================================================================================================== 

            //=*=*=*=*=*=*=*=*=*=*=*=*=*=*=*=*=*=*=*=*=*=*=*=*=*=*=*=*=*=*=*=*=*=*=*=*=*
            return null;//если никаких паттернов не найдено возвращаем нуль
        }

        private List<Dot> CheckPatternMove(int Owner)
        {
            var qry = from Dot d1 in this
                      where d1.Own == Owner && !d1.Blocked
                      from Dot d2 in this
                      where d2.Own == Owner && !d2.Blocked
                              && d2.IndexRelation!=d1.IndexRelation 
                              && Distance(d1, d2) >= 2
                              && Distance(d1, d2) < 3
                      from Dot dm in this
                      where dm.ValidMove & Distance(dm, d1) < 2 
                      && dm.ValidMove & Distance(dm, d2) < 2
                      select dm;
            List<Dot> ld = qry.Distinct(new DotEq()).ToList();
            foreach (Dot d in ld) d.Tag = "CheckPatternMove(" + Owner + ")";
            return ld;
        }

        private List<Dot> EmptyDotsBetween(Dot d1, Dot d2)
        {
            //int dx, dy;
            List<Dot> ld = new List<Dot>();
            if (d1.x - d2.x != 0 && d1.y - d2.y != 0)
            {
                //уравнение прямой (x - x1) / (x2 - x1) = (y - y1) / (y2 - y1)
                var dots = from Dot d in Board_ValidMoves
                           where Math.Abs((d.x - d1.x) / (d2.x - d1.x)) == Math.Abs((d.y - d1.y) / (d2.y - d1.y)) 
                           && d.x >= d1.x && d.x <= d2.x
                           && d.y <= d1.y && d.y >= d2.y
                           select d;
                ld = dots.ToList();
            }
            return ld;
        }

        /// <summary>
        /// проверка хода на гарантированное окружение(когда точки находятся через две клетки) 
        /// </summary>
        /// <param name="Owner"></param>
        /// <returns></returns>
        private List<Dot> CheckPattern2Move(int Owner, bool IndexRelation)
        {
            IEnumerable<Dot> qry;
            if (IndexRelation)
            {
                qry = from Dot d1 in this

                      where d1.Own == Owner && !d1.Blocked
                      from Dot d2 in this
                      where d2.Own == Owner && d2.IndexRelation == d1.IndexRelation && !d2.Blocked && Distance(d1, d2) < 3.5f & Distance(d1, d2) >= 3f
                      from Dot de1 in this
                      where de1.ValidMove & Distance(d1, de1) == 1
                      from Dot de2 in this
                      where de2.ValidMove & Distance(d1, de2) == 1.4f
                      from Dot de3 in this
                      where de3.ValidMove && Distance(d2, de3) < 2
                                          && Distance(d2, de3) < 3.5f
                                          && Distance(de1, de3) < 2 && Distance(de1, de2) < 2
                                          && NeiborDotsSNWE(de3).Where(dt => dt.Own == Owner).Count() <= 1
                      select new Dot(de3.x, de3.y, NumberPattern: 777, Rating: 1);


            }
            else
            {
                qry = from Dot d1 in this
                      where d1.Own == Owner && !d1.Blocked
                      from Dot d2 in this
                      where d2.Own == Owner && !d2.Blocked && Distance(d1, d2) < 3.5f & Distance(d1, d2) >= 3
                      from Dot de1 in this
                      where de1.ValidMove & Distance(d1, de1) == 1
                      from Dot de2 in this
                      where de2.ValidMove & Distance(d1, de2) == 1.4f
                      from Dot de3 in this
                      where de3.ValidMove && Distance(d1, de3) < 2 
                                          && Distance(d2, de3) < 3.5f
                                          && Distance(de1, de3) < 2
                                          && Distance(de1, de2) < 2
                                          && NeiborDotsSNWE(de3).Where(dt=>dt.Own==Owner).Count()<=1
                      select new Dot(de3.x, de3.y, NumberPattern: 777, Rating: 1);

            }
            List<Dot> ld = qry.Distinct(new DotEq()).ToList();
            foreach (Dot d in ld) d.Tag = "CheckPattern2Move(" + Owner + ")";
            return ld;
        }
        private List<Dot> CheckPatternVilka2x2(int Owner, bool IndexRelation) //проверка хода на гарантированное окружение(когда точки находятся через 4 клетки) 
        {
            IEnumerable<Dot> qry;
            //int rating = IndexRelation == true ? 3 : 4;
            if (IndexRelation)
            {
                qry = from Dot d1 in this
                      where d1.Own == Owner && !d1.Blocked

                      from Dot d2 in this
                      where d2.IndexRelation == d1.IndexRelation && !d2.Blocked && Distance(d1, d2) < 4.5f & Distance(d1, d2) >= 2.5f

                      from Dot de1 in this
                      where de1.ValidMove & Distance(d1, de1) == 1

                      from Dot de2 in this
                      where de2.ValidMove & Distance(d2, de2) == 1

                      from Dot de1_1 in this
                      where de1_1.ValidMove & Distance(de1_1, de1) == 1 & Distance(de1_1, d1) < 2

                      from Dot de2_1 in this
                      where de2_1.ValidMove & Distance(de2_1, de2) == 1 & Distance(de2_1, d2) < 2


                      from Dot de3 in this
                      where de3.ValidMove & Distance(de1, de3) < 2
                                          & Distance(de2, de3) < 2
                                          & Distance(de1_1, de3) < 2
                                          & Distance(de2_1, de3) < 2
                                          & Distance(d1, de3) >= 2
                                          & Distance(d2, de3) >= 2

                      select new Dot(de3.x, de3.y, NumberPattern: 777, Rating: 2);
            }
            else
            {
                qry = from Dot d1 in this
                      where d1.Own == Owner && !d1.Blocked

                      from Dot d2 in this
                      where d2.Own == d1.Own && !d2.Blocked && Distance(d1, d2) < 4.5f & Distance(d1, d2) >= 2.5f

                      from Dot de1 in this
                      where de1.ValidMove & Distance(d1, de1) == 1

                      from Dot de2 in this
                      where de2.ValidMove & Distance(d2, de2) == 1

                      from Dot de1_1 in this
                      where de1_1.ValidMove & Distance(de1_1, de1) == 1 & Distance(de1_1, d1) < 2

                      from Dot de2_1 in this
                      where de2_1.ValidMove & Distance(de2_1, de2) == 1 & Distance(de2_1, d2) < 2


                      from Dot de3 in this
                      where de3.ValidMove & Distance(de1, de3) < 2
                                          & Distance(de2, de3) < 2
                                          & Distance(de1_1, de3) < 2
                                          & Distance(de2_1, de3) < 2
                                          & Distance(d1, de3) >= 2
                                          & Distance(d2, de3) >= 2

                      select new Dot(de3.x, de3.y, NumberPattern: 777, Rating: 3);
            }

            List<Dot> ld = qry.Distinct(new DotEq()).ToList();
            foreach (Dot d in ld) d.Tag = "CheckPatternVilka2x2(" + Owner + ")";
            return ld;
        }
        private Dot PickComputerMove(Dot enemy_move, CancellationToken? ct)
        {
#region если первый ход выбираем произвольную соседнюю точку
            if (ListMoves.Count < 2)
            {
                var random = new Random(DateTime.Now.Millisecond);
                var fm = from Dot d in Dots
                         where d.Own == 0 & Math.Sqrt(Math.Pow(Math.Abs(d.x - enemy_move.x), 2) + Math.Pow(Math.Abs(d.y - enemy_move.y), 2)) < 2
                         orderby random.Next()
                         select d;
                return new Dot(fm.First().x, fm.First().y); //так надо чтобы best_move не ссылался на точку в Dots;
            }
#endregion
#region  Если ситуация проигрышная -сдаемся
            //var q1 = from Dot d in Dots
            //         where d.Own == PLAYER_COMPUTER && (d.Blocked == false)
            //         select d;
            //var q2 = from Dot d in Dots
            //         where d.Own == PLAYER_HUMAN && (d.Blocked == false)
            //         select d;
            //float res1 = q2.Count();
            //float res2 = q1.Count();
            //if (res1 / res2 > 2.0)
            //{
            //    return null;
            //}

#endregion

            best_move = null;
            var t1 = DateTime.Now.Millisecond;
            #region StopWatch
#if DEBUG
            stopWatch.Start();
#endif
#endregion
            counter_moves = 0;
            //lst_best_move.Clear();
            lst_branch.Clear();
            
            //Проигрываем разные комбинации
            recursion_depth = 0;
            Play(PLAYER_HUMAN, PLAYER_COMPUTER);

            foreach(Dot d in lst_branch)DebugInfo.lstDBG2.Add(d.ToString() + " - " + d.Tag);
            Dot move = lst_branch.Where(dt => dt.Rating == lst_branch.Min(d => d.Rating)).ElementAtOrDefault(0);

            if (move!=null) best_move = move;
            #region Если не найдено лучшего хода, берем любую точку
            if (best_move == null)
            {
                var random = new Random(DateTime.Now.Millisecond);
                var q = from Dot d in Dots//любая точка
                        where d.Blocked == false & d.Own == PLAYER_NONE 
                        orderby random.Next()
                        select d;

                if (q.Count() > 0) best_move = q.Where(dt => Distance(dt, LastMove) < 3).FirstOrDefault();

                else best_move = q.FirstOrDefault();
                best_move.Tag = "Random";
            }
            #endregion

#region Statistic
#if DEBUG
            stopWatch.Stop();

            DebugInfo.textDBG = "Количество ходов: " + counter_moves + "\r\n Глубина рекурсии: " + MAX_RECURSION +
            "\r\n Ход на " + best_move.ToString() +
            "\r\n время просчета " + stopWatch.ElapsedMilliseconds.ToString() + " мс";
            stopWatch.Reset();
#endif
#endregion
            DebugInfo.lstDBG2.Add("Ход на " + best_move.ToString() + " - " + best_move.Tag);
            return new Dot(best_move.x, best_move.y); //так надо чтобы best_move не ссылался на точку в Dots
        }
        //===============================================================================================
        //-----------------------------------Поиск лучшего хода------------------------------------------
        //===============================================================================================
        private List<Dot> BestMove(int pl1, int pl2)
        {
            DebugInfo.lstDBG1.Clear();
            DebugInfo.lstDBG2.Clear();
            string strDebug = string.Empty;
            List<Dot> moves = new List<Dot>();
            Dot bm;

#if DEBUG
            sW2.Start();
            DebugInfo.textDBG = "CheckMove(pl2,pl1)...";
#endif
            bm = CheckMove(pl2);
            if (DotIndexCheck(bm))
            {
#region DEBUG
#if DEBUG
                {
                    DebugInfo.lstDBG2.Add(bm.x.ToString() + ":" + bm.y.ToString() + " player" + pl1.ToString() + " - Win Comp! " + iNumberPattern.ToString());
                }
#endif
                #endregion
                bm.Tag = "CheckMove(" + pl2 + ")";
                bm.iNumberPattern = 777; //777-ход в результате которого получается окружение -компьютер побеждает
                moves.Add(bm);
                //return bm;
            }
            bm = CheckMove(pl1);
            if (DotIndexCheck(bm))
            {
                #region DEBUG
#if DEBUG
                {
                    DebugInfo.lstDBG2.Add(bm.x + ":" + bm.y + " player" + pl1 + " - Win Human! " + iNumberPattern);
                }
#endif
                #endregion
                bm.Tag = "CheckMove(" + pl1 + ")";
                bm.iNumberPattern = 666; //666-ход в результате которого получается окружение -компьютер проигрывает
                //return bm;
                moves.Add(bm);
            }
#region DEBUG
#if DEBUG
            sW2.Stop();
            strDebug = "CheckMove pl1,pl2 -" + sW2.Elapsed.Milliseconds.ToString();
            sW2.Reset();
            //проверяем паттерны
            sW2.Start();
            DebugInfo.textDBG = "CheckPattern_vilochka проверяем ходы на два вперед...";
#endif
#endregion
#region CheckPattern_vilochka
            bm = CheckPattern_vilochka(pl2);
            //if (bm != null)
            if (DotIndexCheck(bm) && CheckDot(bm, pl2) == false)
            {
#region DEBUG
#if DEBUG
                {
                    DebugInfo.lstDBG2.Add(bm.x + ":" + bm.y + " player" + pl2 + " -CheckPattern_vilochka " + iNumberPattern);
                }
#endif
                #endregion
                bm.Tag = "CheckPattern_vilochka(" + pl2 + ")";
                bm.iNumberPattern = 777; //777-ход в результате которого получается окружение -компьютер побеждает
                moves.Add(bm);//return bm;
            }

            bm = CheckPattern_vilochka(pl1);
            // if (bm != null)
            if (DotIndexCheck(bm) && CheckDot(bm, pl1) == false)
            {
#region DEBUG
#if DEBUG

                {
                    DebugInfo.lstDBG2.Add(bm.x + ":" + bm.y + " player" + pl1 + " -CheckPattern_vilochka " + iNumberPattern);
                }
#endif
                #endregion
                bm.Tag = "CheckPattern_vilochka(" + pl1 + ")";
                bm.iNumberPattern = 666; //777-ход в результате которого получается окружение -компьютер побеждает
                moves.Add(bm);//return bm;
            }
#region DEBUG

#if DEBUG
            sW2.Stop();
            strDebug = strDebug + "\r\nCheckPattern_vilochka -" + sW2.Elapsed.Milliseconds.ToString();
            
            sW2.Reset();
            sW2.Start();
            DebugInfo.textDBG = "CheckPattern2Move...";
#endif
            #endregion
            #endregion

            

            #region CheckPattern2Move проверяем ходы на два вперед на гарантированное окружение

            List<Dot> ld_bm = CheckPattern2Move(pl2, true);
            ld_bm.AddRange(CheckPatternVilka2x2(pl2, true));
            ld_bm.AddRange(CheckPatternVilka2x2(pl2, false));
            ld_bm.AddRange(CheckPattern2Move(pl1, true));
            ld_bm.AddRange(CheckPatternVilka2x2(pl1, true));
            ld_bm.AddRange(CheckPatternVilka2x2(pl1, false));
            if (ld_bm.Count > 0)moves.AddRange(ld_bm);
            
            #region DEBUG
#if DEBUG
            sW2.Stop();
            strDebug = strDebug + "\r\nCheckPattern2Move(pl2) -" + sW2.Elapsed.Milliseconds.ToString();
            sW2.Reset();
            sW2.Start();
            DebugInfo.textDBG = "CheckPatternVilkaNextMove...";
#endif

            #endregion
            #endregion
            #region CheckPatternVilkaNextMove
//            bm = CheckPatternVilkaNextMove(pl2);
//            if (DotIndexCheck(bm))
//            {
//                #region DEBUG
//#if DEBUG
//                {
//                    lstDBG1.Add(bm.x + ":" + bm.y + " player" + pl2 + "CheckPatternVilkaNextMove " + iNumberPattern);
//                }
//#endif
//                #endregion
//                moves.Add(bm); //return bm;
//            }
            #region DEBUG

#if DEBUG
            sW2.Stop();
            strDebug = strDebug + "\r\nCheckPatternVilkaNextMove -" + sW2.Elapsed.Milliseconds.ToString();
            
            sW2.Reset();
            sW2.Start();
            DebugInfo.textDBG = "CheckPattern(pl2)...";
#endif
            #endregion
            #endregion
            #region CheckPattern
            ld_bm = Проверка1(pl2);
            if (ld_bm.Count > 0) moves.AddRange(ld_bm);

            
//            foreach (Dot dt in CheckPattern(pl2))
//            {
//                if (DotIndexCheck(dt))
//                {
//                    #region DEBUG
//#if DEBUG
//                    {
//                        DebugInfo.lstDBG2.Add(dt.x + ":" + dt.y + " player" + pl2 + " - CheckPattern " + dt.iNumberPattern);
//                    }
//#endif
//                    #endregion
//                    if (CheckDot(dt, pl2) == false) moves.Add(dt);
//                }
//            }
//            #region Debug
//#if DEBUG
//            sW2.Stop();
//            strDebug = strDebug + "\r\nCheckPattern(pl2) -" + sW2.Elapsed.Milliseconds.ToString();
//            sW2.Reset();
//            sW2.Start();
//            DebugInfo.textDBG = "CheckPattern(pl1)...";
//#endif
//            #endregion
//            foreach (Dot dt in CheckPattern(pl1))
//            {
//                if (DotIndexCheck(dt))
//                {
//                    #region DEBUG
//#if DEBUG
//                    {
//                        DebugInfo.lstDBG2.Add(dt.x + ":" + dt.y + " player" + pl1 + " - CheckPattern " + dt.iNumberPattern);
//                    }
//#endif
//                    #endregion
//                    if (CheckDot(dt, pl2) == false) moves.Add(dt);
//                }
//            }
//            #region DEBUG
//#if DEBUG
//            sW2.Stop();
//            strDebug = strDebug + "\r\nCheckPattern(pl1) -" + sW2.Elapsed.Milliseconds.ToString();
//            sW2.Reset();
//            sW2.Start();
//            DebugInfo.textDBG = "CheckPatternMove...";
//#endif
//            #endregion
            #endregion
            #region CheckPatternMove
            foreach (Dot dt in CheckPatternMove(pl2))
            {
                if (DotIndexCheck(dt) && (CheckDot(dt, pl2) == false)) moves.Add(dt);
            }
            foreach (Dot dt in CheckPatternMove(pl1))
            {
                if (DotIndexCheck(dt) && (CheckDot(dt, pl1) == false)) moves.Add(dt);
            }

#if DEBUG
            sW2.Stop();
            strDebug = strDebug + "/r/nCheckPatternMove(pl2) -" + sW2.Elapsed.Milliseconds.ToString();
            DebugInfo.textDBG1 = strDebug;
            sW2.Reset();
#endif

#endregion
            var result = moves.Distinct(new DotEq());
            return result.ToList();
        }
        //==================================================================================================================
        
        List<Dot> lst_moves = new List<Dot>();//сюда заносим проверяемые ходы
        List<Dot> lst_branch = new List<Dot>();//сюда заносим начало перспективной ветки
        //
        int counter_moves = 0;
        int res_last_move; //хранит результат хода
        //int recursion_depth;
        const int MAX_RECURSION = 3;
        const int MAX_COUNTMOVES = 3;
        int recursion_depth;
        Dot tempmove;
        //===================================================================================================================
        private int Play(int player1, int player2)//возвращает Owner кто побеждает в результате хода
        {
            List<Dot> lst_best_move = new List<Dot>();//сюда заносим лучшие ходы
            if (recursion_depth==1)counter_moves = 1;
            recursion_depth++;
            counter_moves++;

            if (recursion_depth > MAX_RECURSION) return PLAYER_NONE;

            lst_best_move = BestMove(player1, player2);
            foreach (Dot d in lst_best_move) d.Rating += counter_moves;
            tempmove = lst_best_move.Where(dt => dt.iNumberPattern == 777 & dt.Rating == 1
                                               | dt.iNumberPattern == 666 & dt.Rating == lst_best_move.Min(d => d.Rating)).ElementAtOrDefault(0);

            //если есть паттерн на окружение противника тоже устанавливается бест мув
            if (tempmove != null) 
            {
                lst_branch.Add(tempmove);
                return PLAYER_COMPUTER;
            }
            //если есть паттерн на окружение компа устанавливается бест мув
            tempmove = lst_best_move.Where(dt => dt.iNumberPattern == 666).FirstOrDefault();
            if (tempmove != null) 
            {
                lst_branch.Add(tempmove);
                lst_branch.Last().Rating = tempmove.Rating + counter_moves;
                return PLAYER_HUMAN;
            }

            //            if(lst_best_move.Count>0)
            //            {
            //#region Cycle
            //                foreach (Dot move in lst_best_move.Where(dt=>dt.Rating<2))
            //                {
            //                    #region ходит комп в проверяемые точки
            //                    player2 = player1 == PLAYER_HUMAN ? PLAYER_COMPUTER : PLAYER_HUMAN;
            //                    if (counter_moves > MAX_COUNTMOVES) break;
            //                    //**************делаем ход***********************************
            //                    res_last_move = MakeMove(move,player2);
            //                    lst_moves.Add(move);
            //                    counter_moves++;

            //                    #region проверка на окружение

            //                    if (win_player == PLAYER_COMPUTER)
            //                    {
            //                        Dot dt_move = lst_moves.First();
            //                        dt_move.Rating = counter_moves;
            //                        lst_branch.Add(dt_move);
            //                        UndoMove(move);
            //                        continue;
            //                        //return PLAYER_COMPUTER;
            //                    }
            //                    //если ход в заведомо окруженный регион - пропускаем такой ход
            //                    if (win_player == PLAYER_HUMAN)
            //                    {
            //                        UndoMove(move);
            //                        continue;
            //                    }
            //                    #endregion
            //                    #region Debug statistic
            //#if DEBUG
            //                    DebugInfo.lstDBG1.Add(move);//(move.Own + " -" + move.x + ":" + move.y);
            //                    DebugInfo.textDBG = "Ходов проверено: " + counter_moves +
            //                                       "\r\n проверка вокруг точки " + LastMove +
            //                                       "\r\n время поиска " + stopWatch.ElapsedMilliseconds;
            //#endif
            //#endregion
            //                    //теперь ходит другой игрок ===========================================================================

            //                    int result = Play(player2, player1);

            //                    recursion_depth--;

            //                     if (result == 0)
            //                    {
            //                        lst_moves.Remove(move);
            //                        DebugInfo.lstDBG1.Remove(move);
            //                        UndoMove(move);
            //                        continue;
            //                    }
            //                    else if (result == PLAYER_COMPUTER)
            //                    {
            //                        if (recursion_depth == 1)
            //                        {
            //                            lst_moves[0].Rating = counter_moves;
            //                            lst_branch.Add(lst_moves[0]);
            //                        }

            //                        lst_moves.Remove(move);
            //                        DebugInfo.lstDBG1.Remove(move);
            //                        UndoMove(move);
            //                        return result;
            //                    }
            //                    else if (result == PLAYER_HUMAN)
            //                    {
            //                        if (recursion_depth == 2)
            //                        {
            //                            lst_moves[0].Rating = counter_moves;//такой рейтинг устанавливается если выигрывает человек
            //                            lst_branch.Add(lst_moves[0]);
            //                        }
            //                        lst_moves.Remove(move);
            //                        DebugInfo.lstDBG1.Remove(move);
            //                        UndoMove(move);
            //                        return result;
            //                    }
            //                    if (DebugInfo.lstDBG1.Count > 0) DebugInfo.lstDBG1.RemoveAt(DebugInfo.lstDBG1.Count - 1);
            //                }
            //            #endregion     
            //            }
            //            best_move = lst_best_move.Where(dt => dt.Rating == lst_best_move.Min(d => d.Rating)).ElementAtOrDefault(0);
            lst_branch = lst_best_move;
            return PLAYER_NONE;
        }//----------------------------Play-----------------------------------------------------




        private float SquarePolygon(int nBlockedDots, int nRegionDots)
        {
            return nBlockedDots + nRegionDots / 2.0f - 1;//Формула Пика
        }
//        private void SetLevel(int iLevel = 1)
//        {
//            switch (iLevel)
//            {
//                case 0://easy
//                    SkillLevel = 10;
//                    SkillDepth = 5;
//                    SkillNumSq = 3;
//                    break;
//                case 1://mid
//                    SkillLevel = 30;
//                    SkillDepth = 10;//20;
//                    SkillNumSq = 4;
//                    break;
//                case 2://hard
//                    SkillLevel = 50;
//                    SkillDepth = 50;//50;
//                    SkillNumSq = 2;//5;
//                    break;
//            }
//#if DEBUG
//#endif
//        }
        private int _pause = 10;
        public int pause
        {
            get
            {
                return _pause;
                //
            }
            set
            {
                _pause = value;
            }
        }

        private static void AddToList(List<Dot> ld, IEnumerable<Dot> pattern, int dx, int dy)
        {
            foreach (Dot dot in pattern)
            {
                Dot d = new Dot(dot.x + dx, dot.y + dy);
                if (ld.Contains(d) == false) ld.Add(d);
            }
        }

        public class Pattern
        {
            public int PatternNumber { get; set; }
            List<DotInPattern> _DotsPattern = new List<DotInPattern>();

            public List<DotInPattern> DotsPattern
            {
                get
                {
                    return _DotsPattern;
                }
                set
                {
                    _DotsPattern = value;
                }

            }
            public DotInPattern dXdY_ResultDot = new DotInPattern();
            public Dot FirstDot { get; set; }//Точка отсчета
            public Dot ResultDot
            {
                get
                {
                    return new Dot(FirstDot.x + dXdY_ResultDot.dX, FirstDot.y + dXdY_ResultDot.dY, FirstDot.Own);
                }
            }

            public override string ToString()
            {
                return "Pattern " + PatternNumber.ToString();
            }

        }
        public class DotInPattern
        {
            public int dX { get; set; }
            public int dY { get; set; }
            public string Owner { get; set; }
            public override string ToString()
            {
                return "dX = " + dX.ToString() + "; dY = " + dY.ToString();
            }

        }



        public static class GameMessages
        {
            public static string Message { get; set; }

        }
        //IEnumerator and IEnumerable require these methods.
        public IEnumerator GetEnumerator()
        {
            position = -1;
            //return this;
            return _Dots.GetEnumerator();
        }
        //IEnumerator
        public bool MoveNext()
        {
            position++;
            return (position < _Dots.Count);
        }
        //IEnumerable
        public void Reset()
        { position = 0; }

        public IScore GetScore()
        {
            throw new NotImplementedException();
        }

        public State GetSpaceState(int row, int column)
        {
            throw new NotImplementedException();
        }

        public bool IsValidMove(Dot move)
        {
            return move.ValidMove; 
        }

        public bool IsValidMove(int row, int column)
        {
            return this[row,column].ValidMove; ;
        }

        public bool IsGameOver
        {
            get
            {
                return Board_ValidMoves.Count == 0;
            }
        }

        //public IAsyncOperation<int> MoveAsync(int Player, CancellationToken? cancellationToken, Dot pl_move = null)
        //{
        //    // Use a lock to prevent the ResetAsync method from modifying the game 
        //    // state at the same time that a different thread is in this method.
        //    //lock (_lockObject)
        //    //{
        //    return Move(Player, cancellationToken)
        //}


        public Task<int> Move(int Player, CancellationToken? cancellationToken, Dot pl_move = null)
        {
            var tcs = new TaskCompletionSource<int>();
            Task.Factory.StartNew(() => {
                try
                {
                    var result = MovePlayer(Player, cancellationToken, pl_move);
                    tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            return tcs.Task;
        }

        CancellationTokenSource tokenSource = new CancellationTokenSource();
        //CancellationToken ct;

        /// <summary>
        /// Ход игрока. Возвращает 0 - успешно 
        /// 1 - игра окончена
        /// -1 - ошибка
        /// </summary>
        /// <param name="Player"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="pl_move"></param>
        /// <returns>0 - успешно 
        /// 1 - игра окончена
        /// -1 - ошибка
        /// </returns>
        private int MovePlayer(int Player, CancellationToken? cancellationToken, Dot pl_move = null)
        {
            if (pl_move == null) pl_move = PickComputerMove(LastMove, cancellationToken);
            if (pl_move == null)
            {
                //MessageBox.Show("You win!!! \r\n" + game.Statistic());
                //NewGame();
                return 1;
            }
            pl_move.Own = Player;

            if (MakeMove(pl_move, addForDraw: true) == -1) return -1;

            if (IsGameOver)
            {
                //StatusMsg.textMsg = "Game over! \r\n" + GameEngineUWP.Statistic();
                //await GameEngineUWP.Pause(5);
                //GameEngineUWP.NewGame();
                //StatusMsg.textMsg = "New game started!" + GameEngineUWP.Statistic();
                //await GameEngineUWP.Pause(1);

                return 1;
            }
            //StatusMsg.ColorMsg = player_move == 1 ? GameEngineUWP.colorGamer2 : GameEngineUWP.colorGamer1;
            //StatusMsg.textMsg = player_move == 1 ? "Move computer..." : "Your move!";
            //appView.Title = StatusMsg.textMsg;
            //titleBar.BackgroundColor = StatusMsg.ColorMsg;
            return 0;
            //});

        }

        void IGame.Move(int player, CancellationToken? cancellationToken, Dot pl_move)
        {
            throw new NotImplementedException();
        }

        //IEnumerable
        public object Current
        {
            get
            {
                return _Dots[position];
            }
        }

        public IList<Dot> Board
        {
            get
            {
                throw new NotImplementedException();
            }

            set
            {
                throw new NotImplementedException();
            }
        }

        public State CurrentPlayer
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public State CurrentOpponent
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public State Winner
        {
            get
            {
                throw new NotImplementedException();
            }
        }
    }
}
