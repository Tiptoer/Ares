﻿/*
 Copyright (c) 2015  [Joerg Ruedenauer]
 
 This file is part of Ares.

 Ares is free software; you can redistribute it and/or modify
 it under the terms of the GNU General Public License as published by
 the Free Software Foundation; either version 2 of the License, or
 (at your option) any later version.

 Ares is distributed in the hope that it will be useful,
 but WITHOUT ANY WARRANTY; without even the implied warranty of
 MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 GNU General Public License for more details.

 You should have received a copy of the GNU General Public License
 along with Ares; if not, write to the Free Software
 Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA
 */
using System;
using System.Collections.Generic;
using System.Text;

namespace Ares.Tags
{
    interface IConnectionClient
    {
        void ConnectionChanged(System.Data.Common.DbConnection connection);
    }

    class Schema
    {
        public static readonly int DB_VERSION = 2;

        public static readonly String DBINFO_TABLE = "DBInfo";
        public static readonly String LANGUAGE_TABLE = "Languages";
        public static readonly String LANGUAGENAMES_TABLE = "LanguageNames";
        public static readonly String CATEGORIES_TABLE = "Categories";
        public static readonly String CATEGORYNAMES_TABLE = "CategoryNames";
        public static readonly String FILES_TABLE = "Files";
        public static readonly String TAGS_TABLE = "Tags";
        public static readonly String TAGNAMES_TABLE = "TagNames";
        public static readonly String FILETAGS_TABLE = "FileTags";
        public static readonly String REMOVEDTAGS_TABLE = "RemovedTags";
        public static readonly String FILEEXPORT_TABLE = "FileExport";
        public static readonly String TAGEXPORT_TABLE = "TagExport";

        public static readonly String VERSION_COLUMN = "version";

        public static readonly String ID_COLUMN = "id";
        public static readonly String NAME_COLUMN = "name";

        public static readonly String LC_COLUMN = "code";

        public static readonly String NAMED_LANGUAGE_COLUMN = "namedLanguage";
        public static readonly String LANGUAGE_OF_NAME_COLUMN = "languageOfName";

        public static readonly String LANGUAGE_COLUMN = "language";
        public static readonly String CATEGORY_COLUMN = "category";
        public static readonly String TAG_COLUMN = "tag";

        public static readonly String FILE_COLUMN = "file";
        public static readonly String PATH_COLUMN = "relativePath";
        public static readonly String ARTIST_COLUMN = "artist";
        public static readonly String ALBUM_COLUMN = "album";
        public static readonly String TITLE_COLUMN = "title";
        public static readonly String ACOUST_ID_COLUMN = "acoustId";

        public static readonly String USER_COLUMN = "user";

        public static readonly String GLOBAL_DB_USER = "__global database rpgmusictags.org__";
        public static readonly String ARES_GUI_USER = "Ares Editor";
    }

    partial class SQLiteTagsDB : ITagsDB, ITagsDBFiles, IGlobalTagsDB
    {
        private List<IConnectionClient> m_Clients = new List<IConnectionClient>();

        private Dictionary<int, ITagsDBWriteByLanguage> m_WritesByLanguage = new Dictionary<int, ITagsDBWriteByLanguage>();
        private ITagsDBWrite m_Write = null;
        private ITagsDBTranslations m_Translations = null;
        private ITagsDBRead m_Read = null;
        private Dictionary<int, ITagsDBReadByLanguage> m_ReadsByLanguage = new Dictionary<int, ITagsDBReadByLanguage>();
        private ITagsDBBrowse m_Browser = null;

        public ITagsDBRead ReadInterface
        {
            get 
            {
                if (m_Read == null)
                {
                    TagsDBReading read = new TagsDBReading(m_Connection);
                    m_Clients.Add(read);
                    m_Read = read;
                }
                return m_Read;
            }
        }

        public ITagsDBReadByLanguage GetReadInterfaceByLanguage(int languageId)
        {
            if (!m_ReadsByLanguage.ContainsKey(languageId))
            {
                LanguageReading read = new LanguageReading(languageId, m_Connection);
                m_Clients.Add(read);
                m_ReadsByLanguage[languageId] = read;
                return read;
            }
            else
            {
                return m_ReadsByLanguage[languageId];
            }
        }

        public ITagsDBWrite WriteInterface
        {
            get 
            {
                if (m_Write == null)
                {
                    TagsDBWriting write = new TagsDBWriting(m_Connection);
                    m_Clients.Add(write);
                    m_Write = write;
                }
                return m_Write;
            }
        }

        public ITagsDBWriteByLanguage GetWriteInterfaceByLanguage(int languageId)
        {
            if (!m_WritesByLanguage.ContainsKey(languageId))
            {
                LanguageWriting write = new LanguageWriting(languageId, m_Connection);
                m_Clients.Add(write);
                m_WritesByLanguage[languageId] = write;
                return write;
            }
            else
            {
                return m_WritesByLanguage[languageId];
            }
        }

        public ITagsDBTranslations TranslationsInterface
        {
            get 
            {
                if (m_Translations == null)
                {
                    TagsTranslations translations = new TagsTranslations(m_Connection);
                    m_Clients.Add(translations);
                    m_Translations = translations;
                }
                return m_Translations;
            }
        }

        public ITagsDBFiles FilesInterface
        {
            get { return this; }
        }

        public IGlobalTagsDB GlobalDBInterface
        {
            get { return this; }
        }

        public ITagsDBBrowse BrowseInterface
        {
            get
            {
                if (m_Browser == null)
                {
                    TagsDBBrowsing browser = new TagsDBBrowsing(m_Connection);
                    m_Clients.Add(browser);
                    m_Browser = browser;
                }
                return m_Browser;
            }
        }

    }
}
