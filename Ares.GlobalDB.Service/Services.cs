﻿/*
 Copyright (c) 2013 [Joerg Ruedenauer]
 
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
using ServiceStack.ServiceHost;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Ares.GlobalDB.Services
{
    [Route("/Upload")]
    public class Upload : ServiceStack.ServiceHost.IReturn<UploadResponse>
    {
        public Ares.Tags.TagsExportedData<Ares.Tags.FileIdentification> TagsData { get; set; }
        public String User { get; set; }
        public bool IncludeLog { get; set; }
        public bool Test { get; set; }
    }

    public class UploadResponse
    {
        public int Status { get; set; }
        public String ErrorMessage { get; set; }
        public String Log { get; set; }
    }

    class ServiceDB
    {
        private static String DB_FILE = "GlobalTagsDB.sqlite";
        private static String TEST_FILE = "GlobalTagsDBTest.sqlite";

        public static String DbFile
        {
            get
            {
                return System.Web.HttpContext.Current.Server.MapPath(@"~\App_Data\" + DB_FILE);
            }
        }

        public static String TestFile
        {
            get
            {
                return System.Web.HttpContext.Current.Server.MapPath(@"~\App_Data\" + TEST_FILE);
            }
        }
    }

    class TagsDBUser : IDisposable
    {
        public TagsDBUser(bool test)
        {
            m_TagsDB = Ares.Tags.TagsModule.GetNewTagsDB();
            m_TagsDB.FilesInterface.OpenOrCreateDatabase(test ? ServiceDB.TestFile : ServiceDB.DbFile);
        }

        public Ares.Tags.ITagsDB TagsDB { get { return m_TagsDB; } }

        public void Dispose()
        {
            m_TagsDB.FilesInterface.CloseDatabase();
        }

        private Ares.Tags.ITagsDB m_TagsDB;
    }

    public class UploadService : ServiceStack.ServiceInterface.Service
    {
        public object Post(Upload request)
        {
            using (System.IO.StringWriter writer = new System.IO.StringWriter())
            {
                UploadResponse response = new UploadResponse();
                try
                {
                    // try several times to prevent possible problems with concurrent modifications
                    int retryCount = 0;
                    const int MAX_RETRIES = 5;
                    while (true)
                    {
                        try
                        {
                            int nrOfNewFiles, nrOfNewTags;
                            using (TagsDBUser user = new TagsDBUser(request.Test))
                            {
                                user.TagsDB.GlobalDBInterface.ImportDataFromClient(request.TagsData, request.User, writer, out nrOfNewFiles, out nrOfNewTags);
                            }
                            StatisticsDB.GetStatisticsDB(request.Test).InsertUpload(nrOfNewFiles, nrOfNewTags);
                            response.Status = 0;
                            response.ErrorMessage = String.Empty;
                            break;
                        }
                        catch (Exception)
                        {
                            if (++retryCount >= MAX_RETRIES)
                                throw;
                        }
                    }
                }
                catch (Exception ex)
                {
                    response.Status = 1;
                    response.ErrorMessage = ex.Message;
                }
                String log = writer.ToString();
                if (!request.IncludeLog)
                    log = String.Empty;
                response.Log = log;
                return response;
            }
        }
    }

    [Route("/Download")]
    public class Download : ServiceStack.ServiceHost.IReturn<DownloadResponse>
    {
        public IList<Ares.Tags.FileIdentification> FileIdentification { get; set; }
        public bool Test { get; set; }
    }

    public class DownloadResponse
    {
        public int Status { get; set; }
        public String ErrorMessage { get; set; }
        public Ares.Tags.TagsExportedData<Ares.Tags.FileIdentification> TagsData { get; set; }
        public int NrOfFoundFiles { get; set; }
    }

    public class DownloadService : ServiceStack.ServiceInterface.Service
    {
        public object Post(Download request)
        {
            DownloadResponse response = new DownloadResponse();
            try
            {
                // try several times to prevent possible problems with concurrent modifications
                int retryCount = 0;
                const int MAX_RETRIES = 5;
                while (true)
                {
                    try
                    {
                        int nrOfFoundFiles;
                        using (TagsDBUser user = new TagsDBUser(request.Test))
                        {
                            response.TagsData = user.TagsDB.GlobalDBInterface.ExportDataForFiles(request.FileIdentification, out nrOfFoundFiles);
                            response.NrOfFoundFiles = nrOfFoundFiles;
                        }
                        int nrOfRequestedFiles = request.FileIdentification != null ? request.FileIdentification.Count : 0;
                        StatisticsDB.GetStatisticsDB(request.Test).InsertDownload(nrOfRequestedFiles, nrOfFoundFiles);
                        response.Status = 0;
                        response.ErrorMessage = String.Empty;
                        break;
                    }
                    catch (Exception)
                    {
                        if (++retryCount >= MAX_RETRIES)
                            throw;
                    }
                }
            }
            catch (Exception ex)
            {
                response.Status = 1;
                response.ErrorMessage = ex.Message;
                response.TagsData = new Ares.Tags.TagsExportedData<Ares.Tags.FileIdentification>();
            }
            return response;
        }
    }

    class DataEncoding
    {
        public static String EscapeDataString(String data)
        {
            data = data.Replace("/", "%2F");
            return Uri.EscapeDataString(data);
        }

        public static String UnescapeDataString(String data)
        {
            return data.Replace("%2F", "/");
        }
    }

    public class BrowseRequest
    {
        public String LanguageCode { get; set; }
        public bool Test { get; set; }
        public String WebLanguage { get; set; }
    }

    [Route("Artists")]
    public class AllArtists : BrowseRequest
    {
    }

    [Route("Artists/{Name}")]
    public class Artists : BrowseRequest
    {
        public String Name { get; set; }
    }

    [Route("Albums")]
    public class AllAlbums : BrowseRequest
    {
    }

    [Route("Albums/{Name}")]
    [Route("Artists/{Artist}/{Name}")]
    public class Albums : BrowseRequest
    {
        public String Name { get; set; }
        public String Artist { get; set; }
    }

    [Route("Files")]
    public class AllFiles : BrowseRequest
    {
    }

    [Route("Categories")]
    public class AllCategories : BrowseRequest
    {
    }

    [Route("Tags")]
    public class AllTags : BrowseRequest
    {
    }

    [Route("Categories/{Category}")]
    public class TagsByCategory : BrowseRequest
    {
        public String Category { get; set; }
    }

    [Route("Tags/{Tag}")]
    [Route("Tags/{Category}/{Tag}")]
    public class FilesByTag : BrowseRequest
    {
        public String Category { get; set; }
        public String Tag { get; set; }
    }

    [Route("Files/{Id}")]
    public class TagsByFile : BrowseRequest
    {
        public String Id { get; set; }
        public String Name { get; set; }
    }

    [Route("Statistics")]
    public class Statistics : BrowseRequest
    {
    }

    public class Language
    {
        public String Name { get; set; }
        public String Url { get; set; }
    }

    class LanguageUtil
    {
        public static String GetLanguageCode(String lc, String weblc)
        {
            if (!String.IsNullOrEmpty(lc)) return lc;
            if (!String.IsNullOrEmpty(weblc)) return weblc;
            return "en";
        }
    }

    public class LanguageResponseBase
    {
        protected String m_ReqPath;
        protected IList<Ares.Tags.LanguageForLanguage> m_Languages;

        public String LanguageCode { get; protected set;  }
        public String WebLanguage { get; protected set; }

        protected String AppendParameter(String url, String parameter, String value)
        {
            if (url.LastIndexOf('?') == -1)
                return String.Format("{0}?{1}={2}", url, parameter, value);
            else
                return String.Format("{0}&{1}={2}", url, parameter, value);
        }

        public List<Language> GetDifferentLanguageUrls()
        {
            List<Language> result = new List<Language>();
            String lc = LanguageUtil.GetLanguageCode(LanguageCode, WebLanguage);
            foreach (var language in m_Languages)
            {
                if (language.Code == lc)
                    continue;
                var match = s_LCRegex.Match(m_ReqPath);
                String url = m_ReqPath;
                if (match.Success)
                {
                    url = s_LCRegex.Replace(m_ReqPath, String.Format("LanguageCode={0}", language.Code));
                }
                else
                {
                    url = AppendParameter(m_ReqPath, "LanguageCode", language.Code);
                }
                result.Add(new Language { Name = language.Name, Url = url });
            }
            return result;
        }

        public static System.Text.RegularExpressions.Regex s_LCRegex = new System.Text.RegularExpressions.Regex("LanguageCode=..", System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    }

    public class LanguageResponse : LanguageResponseBase
    {
        public LanguageResponse(String reqPath, IList<Ares.Tags.LanguageForLanguage> languages, String code, String webLanguage)
        {
            m_Languages = languages;
            m_ReqPath = reqPath;
            LanguageCode = code;
            WebLanguage = webLanguage;
        }
    }

    public class BrowseResponse : LanguageResponseBase
    {
        public bool Test { get; private set;  }

        public LanguageResponse LanguageResponse { get { return new LanguageResponse(m_ReqPath, m_Languages, LanguageCode, WebLanguage); } }

        internal void SetOptions(BrowseRequest request, String rawUrl, TagsDBUser dbUser)
        {
            LanguageCode = request.LanguageCode;
            Test = request.Test;
            WebLanguage = request.WebLanguage;
            int languageId = dbUser.TagsDB.TranslationsInterface.GetIdOfLanguage(LanguageUtil.GetLanguageCode(request.LanguageCode, request.WebLanguage));
            m_Languages = dbUser.TagsDB.GetReadInterfaceByLanguage(languageId).GetAllLanguages();
            m_ReqPath = rawUrl;
        }

        protected String MakeBrowseUrl(String url)
        {
            if (!String.IsNullOrEmpty(LanguageCode))
            {
                url = AppendParameter(url, "LanguageCode", LanguageCode);
            }
            if (!String.IsNullOrEmpty(WebLanguage))
            {
                url = AppendParameter(url, "WebLanguage", WebLanguage);
            }
            if (Test)
            {
                url = AppendParameter(url, "Test", "true");
            }
            return url;
        }

    }

    public abstract class ItemResponse<T> : BrowseResponse
    {
        public abstract void SetData(T data);
    }

    public class Artist : ItemResponse<Ares.Tags.Artist>
    {
        private Ares.Tags.Artist Inner { get; set; }
        public override void SetData(Tags.Artist data)
        {
 	        Inner = data;
        }
        public String Name { get { return Inner.Name; } }
        public String Url { get { return MakeBrowseUrl(String.Format("/Artists/{0}", DataEncoding.EscapeDataString(Name))); } }
    }

    public class Album : ItemResponse<Ares.Tags.Album>
    {
        private Ares.Tags.Album Inner { get; set; }
        public override void SetData(Tags.Album data)
        {
 	        Inner = data;
        }
        public String Artist { get { return Inner.Artist; } }
        public String Name { get { return Inner.Name; } }
        public String Url { get { return MakeBrowseUrl(String.Format("/Artists/{0}/{1}", DataEncoding.EscapeDataString(Artist), DataEncoding.EscapeDataString(Name))); } }
        public String ArtistUrl { get { return MakeBrowseUrl(String.Format("/Artists/{0}", DataEncoding.EscapeDataString(Artist))); } }
    }

    public class File : ItemResponse<Ares.Tags.FileIdentification>
    {
        private Ares.Tags.FileIdentification Inner { get; set; }

        public override void SetData(Tags.FileIdentification data)
        {
 	        Inner = data;
        }

        public String Artist { get { return Inner.Artist; } }
        public String Album { get { return Inner.Album; } }
        public String Name { get { return Inner.Title; } }

        public String Url 
        { 
            get 
            {
                String originalUrl = MakeBrowseUrl(String.Format("/Files/{0}", Inner.Id, DataEncoding.EscapeDataString(Name)));
                if (originalUrl.LastIndexOf('?') != -1)
                    return originalUrl + String.Format("&Name={0}", DataEncoding.EscapeDataString(Name));
                else
                    return originalUrl + String.Format("?Name={0}", DataEncoding.EscapeDataString(Name));
            } 
        }
        public String ArtistUrl { get { return MakeBrowseUrl(String.Format("/Artists/{0}", DataEncoding.EscapeDataString(Artist))); } }
        public String AlbumUrl { get { return MakeBrowseUrl(String.Format("/Artists/{0}/{1}", DataEncoding.EscapeDataString(Artist), DataEncoding.EscapeDataString(Album))); } }
    }

    public class Category : ItemResponse<String>
    {
        public String Name { get; private set; }
        public override void SetData(string data)
        {
 	        Name = data;
        }
        public String Url { get { return MakeBrowseUrl(String.Format("/Categories/{0}", DataEncoding.EscapeDataString(Name))); } }
    }

    public class Tag : BrowseResponse
    {
        public String Name { get; set; }
        public String Category { get; set; }

        public String Url { get { return MakeBrowseUrl(String.Format("/Tags/{0}/{1}", DataEncoding.EscapeDataString(Category), DataEncoding.EscapeDataString(Name))); } }
        public String CategoryUrl { get { return MakeBrowseUrl(String.Format("/Categories/{0}", DataEncoding.EscapeDataString(Category))); } }
    }

    public class ListResponse<T> : ItemResponse<List<T>>
    {
        public List<T> Results { get; private set; }
        public override void SetData(List<T> data)
        {
 	        Results = data;
        }
    }

    public class AllArtistsResponse : ListResponse<Artist>
    {
    }

    public class AllAlbumsResponse : ListResponse<Album>
    {
    }

    public class ArtistsResponse : ListResponse<Album>
    {
        public String Artist { get; set; }
        public String ArtistUrl { get { return MakeBrowseUrl(String.Format("/Artists/{0}", DataEncoding.EscapeDataString(Artist))); } }
    }

    public class AlbumsResponse : ListResponse<File>
    {
        public String Artist { get; set; }
        public String Album { get; set; }
    }

    public class AllFilesResponse : ListResponse<File>
    {
    }

    public class AllCategoriesResponse : ListResponse<Category>
    {
    }

    public class AllTagsResponse : ListResponse<Tag>
    {
    }

    public class TagsByCategoryResponse : ListResponse<Tag>
    {
        public String Category { get; set; }
    }

    public class TagsByFileResponse : ListResponse<Tag>
    {
        public String File { get; set; }
    }

    public class FilesByTagResponse : ListResponse<File>
    {
        public String Tag { get; set; }
    }

    public class StatisticsResponse : ItemResponse<Ares.Tags.Statistics>
    {
        public int FilesCount { get { return Inner.FilesCount; } }

        public int AlbumsCount { get { return Inner.AlbumsCount; } }

        public int ArtistsCount { get { return Inner.ArtistsCount; } }

        public int TagsCount { get { return Inner.TagsCount; } }

        public int CategoriesCount { get { return Inner.CategoriesCount; } }

        public int UsersCount { get { return Inner.UsersCount; } }

        public double AvgTagsPerFile { get { return Inner.AvgTagsPerFile; } }

        public String AlbumsUrl { get { return MakeBrowseUrl("/Albums"); } }

        public String ArtistsUrl { get { return MakeBrowseUrl("/Artists"); } }

        public String FilesUrl { get { return MakeBrowseUrl("/Files"); } }

        public String TagsUrl { get { return MakeBrowseUrl("/Tags"); } }

        public String CategoriesUrl { get { return MakeBrowseUrl("/Categories"); } }

        private Ares.Tags.Statistics Inner { get; set; }
        
        public override void SetData(Ares.Tags.Statistics data)
        {
 	        Inner = data;
        }

        public UploadStatistics UploadStats { get; set; }

        public DownloadStatistics DownloadStats { get; set; }
    }

    public class BrowsingService : ServiceStack.ServiceInterface.Service
    {
        private void SetResponseOptions(BrowseRequest request, BrowseResponse response, TagsDBUser dbUser)
        {
            var httpReq = base.RequestContext.Get<IHttpRequest>();
            response.SetOptions(request, httpReq.RawUrl, dbUser);
        }

        private T CreateItemResponse<T, U>(BrowseRequest request, U inner, TagsDBUser dbUser) where T : ItemResponse<U>, new()
        {
            T t = new T();
            t.SetData(inner);
            SetResponseOptions(request, t, dbUser);
            return t;
        }

        private object CreateHttpResponse<T>(T request, object innerResponse) where T : BrowseRequest
        {
            String view = request.GetType().Name;
            if (!String.IsNullOrEmpty(request.WebLanguage))
            {
                view += "_" + request.WebLanguage;
            }
            return new ServiceStack.Common.Web.HttpResult(innerResponse)
            {
                View = view
            };
        }

        public object Get(AllArtists request)
        {
            List<Artist> result = new List<Artist>();
            using (TagsDBUser user = new TagsDBUser(request.Test))
            {
                foreach (Ares.Tags.Artist artist in user.TagsDB.BrowseInterface.GetAllArtists())
                {
                    result.Add(CreateItemResponse<Artist, Ares.Tags.Artist>(request, artist, user));
                }
                return CreateHttpResponse(request, CreateItemResponse<AllArtistsResponse, List<Artist>>(request, result, user));
            }
        }

        public object Get(AllAlbums request)
        {
            List<Album> result = new List<Album>();
            using (TagsDBUser user = new TagsDBUser(request.Test))
            {
                foreach (Ares.Tags.Album album in user.TagsDB.BrowseInterface.GetAllAlbums())
                {
                    result.Add(CreateItemResponse<Album, Ares.Tags.Album>(request, album, user));
                }
                return CreateHttpResponse(request, CreateItemResponse<AllAlbumsResponse, List<Album>>(request, result, user));
            }
        }

        public object Get(Artists request)
        {
            List<Album> result = new List<Album>();
            using (TagsDBUser user = new TagsDBUser(request.Test))
            {
                foreach (Ares.Tags.Album album in user.TagsDB.BrowseInterface.GetAlbumsByArtist(DataEncoding.UnescapeDataString(request.Name)))
                {
                    result.Add(CreateItemResponse<Album, Ares.Tags.Album>(request, album, user));
                }
                var response = CreateItemResponse<ArtistsResponse, List<Album>>(request, result, user);
                response.Artist = DataEncoding.UnescapeDataString(request.Name);
                return CreateHttpResponse(request, response);
            }
        }

        public object Get(Albums request)
        {
            List<File> result = new List<File>();
            using (TagsDBUser user = new TagsDBUser(request.Test))
            {
                foreach (Ares.Tags.FileIdentification file in user.TagsDB.BrowseInterface.GetFilesByAlbum(DataEncoding.UnescapeDataString(request.Artist), DataEncoding.UnescapeDataString(request.Name)))
                {
                    result.Add(CreateItemResponse<File, Ares.Tags.FileIdentification>(request, file, user));
                }
                var response = CreateItemResponse<AlbumsResponse, List<File>>(request, result, user);
                response.Album = DataEncoding.UnescapeDataString(request.Name);
                response.Artist = DataEncoding.UnescapeDataString(request.Artist);
                return CreateHttpResponse(request, response);
            }
        }

        public object Get(AllFiles request)
        {
            List<File> result = new List<File>();
            using (TagsDBUser user = new TagsDBUser(request.Test))
            {
                foreach (Ares.Tags.FileIdentification file in user.TagsDB.BrowseInterface.GetAllFiles())
                {
                    result.Add(CreateItemResponse<File, Ares.Tags.FileIdentification>(request, file, user));
                }
                return CreateHttpResponse(request, CreateItemResponse<AllFilesResponse, List<File>>(request, result, user));
            }
        }

        public object Get(AllCategories request)
        {
            List<Category> result = new List<Category>();
            using (TagsDBUser user = new TagsDBUser(request.Test))
            {
                var readIf = GetReadInterface(user, request);
                foreach (var categoryForLanguage in readIf.GetAllCategories())
                {
                    result.Add(CreateItemResponse<Category, String>(request, categoryForLanguage.Name, user));
                }
                return CreateHttpResponse(request, CreateItemResponse<AllCategoriesResponse, List<Category>>(request, result, user));
            }
        }

        public object Get(AllTags request)
        {
            List<Tag> result = new List<Tag>();
            using (TagsDBUser user = new TagsDBUser(request.Test))
            {
                var readIf = GetReadInterface(user, request);
                foreach (var tagForLanguage in readIf.GetAllTags())
                {
                    var tag = new Tag { Name = tagForLanguage.Name, Category = tagForLanguage.Category };
                    SetResponseOptions(request, tag, user);
                    result.Add(tag);
                }
                return CreateHttpResponse(request, CreateItemResponse<AllTagsResponse, List<Tag>>(request, result, user));
            }
        }

        public object Get(TagsByCategory request)
        {
            List<Tag> result = new List<Tag>();
            using (TagsDBUser user = new TagsDBUser(request.Test))
            {
                var readIf = GetReadInterface(user, request);
                foreach (var categoryForLanguage in readIf.GetAllCategories())
                {
                    String category = DataEncoding.UnescapeDataString(request.Category);
                    if (categoryForLanguage.Name.Equals(category, StringComparison.Ordinal))
                    {
                        foreach (var tagForLanguage in readIf.GetAllTags(categoryForLanguage.Id))
                        {
                            var tag = new Tag { Name = tagForLanguage.Name, Category = request.Category };
                            SetResponseOptions(request, tag, user);
                            result.Add(tag);
                        }
                        break;
                    }
                }
                var response = CreateItemResponse<TagsByCategoryResponse, List<Tag>>(request, result, user);
                response.Category = DataEncoding.UnescapeDataString(request.Category);
                return CreateHttpResponse(request, response);
            }
        }

        public object Get(TagsByFile request)
        {
            List<Tag> result = new List<Tag>();
            Int64 fileId = Int64.Parse(request.Id);
            using (TagsDBUser user = new TagsDBUser(request.Test))
            {
                var readIf = GetReadInterface(user, request);
                foreach (var tagForLanguage in readIf.GetTagsForFile(fileId))
                {
                    var tag = new Tag { Name = tagForLanguage.Name, Category = tagForLanguage.Category};
                    SetResponseOptions(request, tag, user);
                    result.Add(tag);
                }
                var response = CreateItemResponse<TagsByFileResponse, List<Tag>>(request, result, user);
                response.File = DataEncoding.UnescapeDataString(request.Name);
                return CreateHttpResponse(request, response);
            }
        }

        public object Get(FilesByTag request)
        {
            List<File> result = new List<File>();
            using (TagsDBUser user = new TagsDBUser(request.Test))
            {
                long tagId = GetReadInterface(user, request).FindTag(DataEncoding.UnescapeDataString(request.Category), DataEncoding.UnescapeDataString(request.Tag));
                foreach (var file in user.TagsDB.ReadInterface.GetFilesForTag(tagId))
                {
                    result.Add(CreateItemResponse<File, Ares.Tags.FileIdentification>(request, file, user));
                }
                var response = CreateItemResponse<FilesByTagResponse, List<File>>(request, result, user);
                response.Tag = DataEncoding.UnescapeDataString(request.Tag);
                return CreateHttpResponse(request, response);
            }
        }

        public object Get(Statistics request)
        {
            Ares.Tags.Statistics result = new Ares.Tags.Statistics();
            using (TagsDBUser user = new TagsDBUser(request.Test))
            {
                result = user.TagsDB.BrowseInterface.GetStatistics();
                var response = CreateItemResponse<StatisticsResponse, Ares.Tags.Statistics>(request, result, user);
                UploadStatistics uploadStats; DownloadStatistics downloadStats;
                StatisticsDB.GetStatisticsDB(request.Test).GetStatistics(out uploadStats, out downloadStats);
                response.UploadStats = uploadStats;
                response.DownloadStats = downloadStats;
                return CreateHttpResponse(request, response);
            }
        }

        private Ares.Tags.ITagsDBReadByLanguage GetReadInterface(TagsDBUser user, BrowseRequest request)
        {
            String lc = LanguageUtil.GetLanguageCode(request.LanguageCode, request.WebLanguage);
            int id = user.TagsDB.TranslationsInterface.GetIdOfLanguage(lc);
            return user.TagsDB.GetReadInterfaceByLanguage(id);
        }

    }

    [Route("Start")]
    class Empty
    {
    }

    class EmptyResponse
    {
    }

    class RootService : ServiceStack.ServiceInterface.Service
    {
        public object Get(Empty request)
        {
            if (base.Request.Headers["Accept-Language"] != null && base.Request.Headers["Accept-Language"].StartsWith("de", StringComparison.InvariantCultureIgnoreCase))
            {
                return new ServiceStack.Common.Web.HttpResult(new EmptyResponse())
                {
                    View = "Start_de"
                };
            }
            else
            {
                return new ServiceStack.Common.Web.HttpResult(new EmptyResponse())
                {
                    View = "Start"
                };
            }
        }
    }
}
