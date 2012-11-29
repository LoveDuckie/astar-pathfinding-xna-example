using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.IO;
using System.Text;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;

namespace Pathfinding
{
    // For enabling the demonstration to know what to draw.
    public enum GridType
    {
        Free = 1,
        Block,
        Start,
        End
    }

    public class Game1 : Microsoft.Xna.Framework.Game
    {
        private class GridNode : IEquatable<GridNode>
        {
            //objects required
            public Point Position { get; set; }
            public GridNode Parent { get; set; }
            public GridType Type { get; set; }

            // Makes it quicker when having to choose
            public bool Explored { get; set; }

            //calculating path costs
            public int G_Cost { get; set; }
            public int H_Cost { get; set; }
            public int F_Cost { get; set; }

            //values for marking
            public bool inClosedList { get; set; }
            public bool inOpenList { get; set; }

            public GridNode()
            {
                Position = new Point(0, 0);
                Type     = GridType.Free;
                Parent   = null;
                G_Cost = 0;
                H_Cost = 0;
                F_Cost = 999;
            }

            public void Recalculate()
            {

                F_Cost = H_Cost + G_Cost;
            }

            public bool Equals(GridNode _node)
            {
                return (this.Position == _node.Position &&
                        this.Parent == _node.Parent &&
                        this.Type == _node.Type);
            }
        }

        private List<GridNode> _pathlist;

        private bool _pathfound;

        // The sprites that are to be loaded in
        private Dictionary<string, Texture2D> _tilesprites;

        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;

        private GridType[,] _tile_grid;
        private GridType[,] _tile_overlay;

        private GridNode[,] _nodes;

        // Dealing with states now.
        private KeyboardState _prev_keystate;
        private KeyboardState _current_keystate;

        private Point _start_point;

        private Point _end_point;

        SpriteFont _debugfont;

        public Game1()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";

            graphics.PreferredBackBufferHeight = 600;
            graphics.PreferredBackBufferWidth = 800;
        }

        // A quick way of generating the heuristic from the given point and the
        // goal point
        public int GenerateHeuristic(Point start, Point end)
        {
            return (((Math.Abs(start.X - end.X)) + (Math.Abs(start.Y - end.Y))) * 10);
        }

        public Point GetLowestF()
        {
            int _lowestscore = 0;
            Point _coords = new Point();

            for (int i = 0; i < _nodes.GetLength(0); i++)
            {
                for (int j = 0; j < _nodes.GetLength(1); j++)
                {
                    if (_lowestscore == 0 && (_nodes[i, j].F_Cost != 0 && _nodes[i,j].inOpenList))
                    {
                        _lowestscore = _nodes[i, j].F_Cost;
                        _coords.X = i;
                        _coords.Y = j;
                    }
                    else if (_nodes[i, j].F_Cost < _lowestscore && _nodes[i, j].inOpenList)
                    {
                        _lowestscore = _nodes[i, j].F_Cost;
                        _coords.X = i;
                        _coords.Y = j;
                    }
                }
            }

            return _coords;
        }

        // Find the G score between the two points
        // Presumably they are next to each other.
        public int DistanceBetween(Point a, Point b)
        {
            // Is it a diagonal direction
            // that the point is going in?
            if ((Math.Abs(a.X - b.X) > 0) && 
                (Math.Abs(a.Y - b.Y) > 0))
            {
                return 14;
            }
            else
            {
                return 10;
            }           
        }

        // Done at the beginning of the application run cycle.
        protected override void Initialize()
        {

            this.IsMouseVisible = true;

            _tilesprites = new Dictionary<string, Texture2D>();

            _pathlist = new List<GridNode>();

            _tile_grid = new GridType[16,14];
            _tile_overlay = new GridType[16, 14];

            _nodes = new GridNode[16, 14];

            int _level_width = _nodes.GetLength(0);
            int _level_height = _nodes.GetLength(1);

            // Initialise all of the tiles in the map.
            for (int i = 0; i < _tile_grid.GetLength(0); i++)
            {
                for (int j = 0; j < _tile_grid.GetLength(1); j++)
                {
                    _tile_grid[i, j] = GridType.Free;
                    _nodes[i, j] = new GridNode() {F_Cost = 0, G_Cost = 0, H_Cost = 0, inOpenList = false, inClosedList = false, Position = new Point(i, j), Parent = null };
                }
            }


            // Defining areas that the path can't go.
            _tile_grid[4, 2] = GridType.Block;
            _tile_grid[4, 3] = GridType.Block;
            _tile_grid[4, 1] = GridType.Block;
            _tile_grid[6, 6] = GridType.Block;
            _tile_grid[5, 6] = GridType.Block;
            _tile_grid[4, 6] = GridType.Block;
            _tile_grid[3, 6] = GridType.Block;
            _tile_grid[2, 6] = GridType.Block;
            _tile_grid[7, 6] = GridType.Block;
            _tile_grid[8, 6] = GridType.Block;
            _tile_grid[9, 6] = GridType.Block;
            _tile_grid[10, 6] = GridType.Block;
            _tile_grid[15, 10] = GridType.Block;
            _tile_grid[13, 10] = GridType.Block;
            _tile_grid[14, 9] = GridType.Block;
            _tile_grid[13, 9] = GridType.Block;
            _tile_grid[15, 9] = GridType.Block;



            _start_point = new Point(2, 2);
            _end_point = new Point(14, 10);

            // Start the pathfinding.
            _nodes[_start_point.X, _start_point.Y].inOpenList = true;
            _nodes[_start_point.X, _start_point.Y].G_Cost = 0;
            _nodes[_start_point.X, _start_point.Y].H_Cost = GenerateHeuristic(_start_point, _end_point);
            _nodes[_start_point.X, _start_point.Y].F_Cost = _nodes[_start_point.X, _start_point.Y].G_Cost + _nodes[_start_point.X, _start_point.Y].H_Cost;            

            // While the open list is not empty.
            while (IsOpenNodes())
            {
                Point _lowest = GetLowestF();

                // If we've reached the end, do a bit of recursion to
                // construct the newly made path.
                if (_lowest == _end_point)
                {
                    ConstructPath(_lowest);
                    break;
                }

                _nodes[_lowest.X, _lowest.Y].inOpenList = false;
                _nodes[_lowest.X, _lowest.Y].inClosedList = true;

                // Looping through the neighbours now.
                for (int g = (_lowest.X - 1); g < (_lowest.X + 2); g++)
                {
                    for (int f = (_lowest.Y - 1); f < (_lowest.Y + 2); f++)
                    {

                        if (CheckClear(g, f) == true)
                        {
                            // Don't care if it's in the closed list.
                            // Also don't care if it's out of the bounds of level or even if there's a rock there.
                            if (!_nodes[g, f].inClosedList)
                            {

                                int _tentative_g_score = _nodes[g, f].G_Cost + DistanceBetween(_lowest, new Point(g, f));
                                bool _tentative_is_better = false;

                                if (!_nodes[g, f].inOpenList)
                                {
                                    _nodes[g, f].inOpenList = true;
                                    _nodes[g, f].H_Cost = GenerateHeuristic(_nodes[g, f].Position, _end_point);
                                    _tentative_is_better = true;
                                }
                                else if (_tentative_g_score < _nodes[g, f].G_Cost) // we want the gscore to be as low as possible
                                {                                                  // so if the 
                                    _tentative_is_better = true;
                                }
                                else
                                {
                                    _tentative_is_better = false;
                                }

                                if (_tentative_is_better)
                                {
                                    _nodes[g, f].Parent = _nodes[_lowest.X, _lowest.Y];
                                    _nodes[g, f].G_Cost = _tentative_g_score;
                                    _nodes[g, f].F_Cost = _nodes[g, f].G_Cost + _nodes[g, f].H_Cost;
                                }
                            }
                        }
                    }
                }
            }

            base.Initialize();
        }



        // The grid node to construct from.
        // Loop back recursively on the parent until the path is formed.
        public void ConstructPath(Point _construct_from)
        {
            if (_nodes[_construct_from.X, _construct_from.Y].Parent != null)
            {
                _pathlist.Add(_nodes[_construct_from.X, _construct_from.Y]);
                ConstructPath(new Point((int)_nodes[_construct_from.X, _construct_from.Y].Parent.Position.X,
                                        (int)_nodes[_construct_from.X, _construct_from.Y].Parent.Position.Y));
            }
        }

        // Check that there is at least one node in the open list.
        public bool IsOpenNodes()
        {
            for (int i = 0; i < _nodes.GetLength(0); i++)
            {
                for (int j = 0; j < _nodes.GetLength(1); j++)
                {
                    if (_nodes[i, j].inOpenList)
                        return true;
                }
            }

            return false;
        }




        // Load the level using the binary file that would have been generated otherwise.
        private void LoadLevel()
        {
            try
            {
                BinaryReader _levelreader = new BinaryReader(new FileStream("level.dat", FileMode.Open), Encoding.ASCII);

                int _size = (int)_levelreader.BaseStream.Length;

                byte[] _data = _levelreader.ReadBytes(_size);

                for (int i = 0; i < _data.Length; i++)
                {

                }

                _levelreader.Close();

            }
            catch (IOException exception)
            {
                Console.WriteLine(exception.Message.ToString());
            }
        }

        // Clear ou the level
        private void ClearLevel()
        {
            for (int i = 0; i < _tile_grid.GetLength(0); i++)
            {
                for (int j = 0; j < _tile_grid.GetLength(1); j++)
                {
                    _tile_grid[i,j] = GridType.Free;
                }
            }
        }

        private void SaveLevel()
        {
            BinaryWriter _levelwriter = new BinaryWriter(new FileStream("level.dat", FileMode.Create),
                                                         Encoding.ASCII);

            for (int i = 0; i < _tile_grid.GetLength(0); i++)
            {
                for (int j = 0; j < _tile_grid.GetLength(1); j++)
                {
                    _levelwriter.Write((byte)i);
                    _levelwriter.Write((byte)j);
                    _levelwriter.Write((byte)_tile_grid[i, j]);
                }
            }

            _levelwriter.Flush();
            _levelwriter.Close();
        }

        // Make sure that the position is legal for the usage of the map.   
        // Make sure that it is within the bounds of the map also.
        private bool CheckClear(int x, int y)
        {
            if (x > 0 && x < _tile_grid.GetLength(0) &&
                y > 0 && y < _tile_grid.GetLength(1))
            {
                if (_tile_grid[x, y] == GridType.Free)
                {
                    return true;
                }
                else
                {
                    return false;
                }
             }

            return false;
        }

        protected override void LoadContent()
        {
            spriteBatch = new SpriteBatch(GraphicsDevice);

            // Load in the content for the game
            _tilesprites.Add("StartTile", Content.Load<Texture2D>("tile_start"));
            _tilesprites.Add("EndTile", Content.Load<Texture2D>("tile_finish"));
            _tilesprites.Add("BlockTile", Content.Load<Texture2D>("tile_rock"));
            _tilesprites.Add("GrassTile", Content.Load<Texture2D>("tile_grass"));
            _tilesprites.Add("OpenGrid", Content.Load<Texture2D>("open_grid"));
            _tilesprites.Add("CloseGrid", Content.Load<Texture2D>("close_grid"));
            _tilesprites.Add("PathDot", Content.Load<Texture2D>("path_dot"));

            _debugfont = Content.Load<SpriteFont>("Debug_Font");
        }

        protected override void UnloadContent()
        {
        
        }

        protected override void Update(GameTime gameTime)
        {
            this._current_keystate = Keyboard.GetState();

            // Allows the game to exit
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
                this.Exit();

            if (Keyboard.GetState().IsKeyDown(Keys.Escape))
                this.Exit();


            this._prev_keystate = Keyboard.GetState();

            base.Update(gameTime);
        }


        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);

            int _tileWidth = _tilesprites["GrassTile"].Width;
            int _tileHeight = _tilesprites["GrassTile"].Height;
            spriteBatch.Begin();

            // Display the grid that we are going to show.
            for (int i = 0; i < _tile_grid.GetLength(0); i++)
            {
                for (int j = 0; j < _tile_grid.GetLength(1); j++)
                {
                    switch (_tile_grid[i, j])
                    {
                        case GridType.Start:
                            spriteBatch.Draw(_tilesprites["StartTile"], new Vector2(i * _tileWidth, j * _tileHeight), Color.White);
                            break;

                        case GridType.End:
                            spriteBatch.Draw(_tilesprites["EndTile"], new Vector2(i * _tileWidth, j * _tileHeight), Color.White);
                            break;

                        case GridType.Block:

                            spriteBatch.Draw(_tilesprites["BlockTile"], new Vector2(i * _tileWidth, j * _tileHeight), Color.White);
                            break;

                        case GridType.Free:

                            spriteBatch.Draw(_tilesprites["GrassTile"], new Vector2(i * _tileWidth, j * _tileHeight), Color.White);
                            break;

                    }
                }
            }


            // y u no work?


            //for (int k = 0; k < _openlist.Count; k++)
            //{
            //    spriteBatch.Draw(_tilesprites["OpenGrid"], new Vector2(_openlist.ElementAt<GridNode>(k).Position.X * _tileWidth, _openlist.ElementAt<GridNode>(k).Position.Y * _tileHeight), Color.White);
                
            //    // Output the F Cost.
            //    spriteBatch.DrawString(_debugfont, _openlist.ElementAt<GridNode>(k).F_Cost.ToString(),
            //                           new Vector2(_openlist.ElementAt<GridNode>(k).Position.X * _tileWidth, _openlist.ElementAt<GridNode>(k).Position.Y * _tileHeight),
            //                           Color.White);

            //    // Output the H Cost.
            //    spriteBatch.DrawString(_debugfont, _openlist.ElementAt<GridNode>(k).H_Cost.ToString(),
            //                           new Vector2((_openlist.ElementAt<GridNode>(k).Position.X * _tileWidth) + (_tileWidth - 30), (_openlist.ElementAt<GridNode>(k).Position.Y * _tileHeight) + (_tileHeight - 20)),
            //           Color.White);

            //    // Output the G Cost.
            //    spriteBatch.DrawString(_debugfont, _openlist.ElementAt<GridNode>(k).G_Cost.ToString(),
            //                           new Vector2(_openlist.ElementAt<GridNode>(k).Position.X * _tileWidth,(_openlist.ElementAt<GridNode>(k).Position.Y * _tileHeight) + (_tileHeight - 20)),
            //           Color.White);
            //}

            //for (int m = 0; m < _closedlist.Count; m++)
            //{
            //    spriteBatch.Draw(_tilesprites["CloseGrid"], new Vector2(_closedlist.ElementAt<GridNode>(m).Position.X * _tileWidth, _closedlist.ElementAt<GridNode>(m).Position.Y * _tileHeight), Color.White);
            //}
    
            //Go through the grid and output the drawing.
            for (int o = 0; o < _nodes.GetLength(0); o++)
            {
                for (int p = 0; p < _nodes.GetLength(1); p++)
                {
                    if (_nodes[o, p].inOpenList)
                    {
                        spriteBatch.Draw(_tilesprites["OpenGrid"], new Vector2(_nodes[o, p].Position.X * _tileWidth, _nodes[o, p].Position.Y * _tileHeight), Color.White);
                    }
                    else if (_nodes[o, p].inClosedList)
                    {
                        spriteBatch.Draw(_tilesprites["CloseGrid"], new Vector2(_nodes[o, p].Position.X * _tileWidth, _nodes[o, p].Position.Y * _tileHeight), Color.White);
                    }

                    if (_nodes[o, p].inClosedList || _nodes[o, p].inOpenList)
                    {
                        spriteBatch.DrawString(_debugfont, _nodes[o, p].F_Cost.ToString(),
                                               new Vector2(_nodes[o, p].Position.X * _tileWidth, _nodes[o, p].Position.Y * _tileHeight),
                                               Color.White);

                        // Output the H Cost.
                        spriteBatch.DrawString(_debugfont, _nodes[o, p].H_Cost.ToString(),
                                               new Vector2((_nodes[o, p].Position.X * _tileWidth) + (_tileWidth - 30), (_nodes[o, p].Position.Y * _tileHeight) + (_tileHeight - 20)),
                               Color.White);

                        // Output the G Cost.
                        spriteBatch.DrawString(_debugfont, _nodes[o, p].G_Cost.ToString(),
                                               new Vector2(_nodes[o, p].Position.X * _tileWidth, (_nodes[o, p].Position.Y * _tileHeight) + (_tileHeight - 20)),
                               Color.White);
                    }
                }
            }

            for (int s = 0; s < _pathlist.Count; s++)
            {
                spriteBatch.Draw(_tilesprites["PathDot"], new Vector2(_pathlist[s].Position.X * _tileWidth,_pathlist[s].Position.Y * _tileHeight), Color.White);
            }

            spriteBatch.End();
            
            base.Draw(gameTime);
        }
    }
}
