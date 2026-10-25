/***************************************************************************
 *   Copyright (C) 2008 by Mootilda                                        *
 *   http://Mootilda.ModTheSims2.com                                       *
 *                                                                         *
 *   This program is free software; you can redistribute it and/or modify  *
 *   it under the terms of the GNU General Public License as published by  *
 *   the Free Software Foundation; either version 2 of the License, or     *
 *   (at your option) any later version.                                   *
 *                                                                         *
 *   This program is distributed in the hope that it will be useful,       *
 *   but WITHOUT ANY WARRANTY; without even the implied warranty of        *
 *   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the         *
 *   GNU General Public License for more details.                          *
 *                                                                         *
 *   You should have received a copy of the GNU General Public License     *
 *   along with this program; if not, write to the                         *
 *   Free Software Foundation, Inc.,                                       *
 *   59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.             *
 ***************************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using SimPe.Interfaces.Files;
using SimPe.Packages;

namespace HoodReplace
{
    // Base class for TS2 neighborhood or SC4 city files.
    public class R_Terrain
    {
        public R_Terrain()
        {
        }

        public virtual int Width
        {
            get
            {
                return 0;
            }
        }

        public virtual int Height
        {
            get
            {
                return 0;
            }
        }

        public virtual byte[] Raw
        {
            get
            {
                return null;
            }
            set
            {
            }
        }

        public virtual float[,] GetTerrain(int iTop, int iLeft, int iHeight, int iWidth)
        {
            return null;
        }

        public virtual void ReplaceTerrain(int iTop, int iLeft, int iHeight, int iWidth, float[,] fTerrain)
        {
        }
    }
}