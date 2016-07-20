﻿using Ares.AudioSource;
using Ares.Data;
using Ares.ModelInfo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Ares.AudioSource
{
    /// <summary>
    /// A file provided by an audio source
    /// </summary>
    public interface IDownloadableAudioFile<out T> where T : IAudioSource
    {
        string Filename { get; }

        /// <summary>
        /// Size of this download.
        /// Any unit can be used for the values returned by this property, but the unit should
        /// be consistent within all Files/Results from a single IAudioSource
        /// </summary>
        double? DownloadSize { get; }

        /// <summary>
        /// Download this object (including anything that is required, i.e. audio files required by an IModeElementSearchResult).
        /// All audio files will be placed at the given relative path beneath either the sounds or music directory - depending on their type.
        /// </summary>
        /// <returns></returns>
        AudioDownloadResult Download(IAbsoluteProgressMonitor monitor, ITargetDirectoryProvider targetDirectoryProvider);

        string SourceUrl { get; }

        SoundFileType FileType { get; }

        T AudioSource { get; }
    }

    /// <summary>
    /// This interface is resposible for defining where on disk & within the ARES library a IDownloadableAudioFile should be 
    /// placed.
    /// 
    /// The actual implementation may define where within the libraries (if at all) each file will be placed.
    /// </summary>
    public interface ITargetDirectoryProvider
    {
        /// <summary>
        /// Return the relative path to the folder (within the appropriate ARES library) where the given audio file will be placed.
        /// </summary>
        /// <param name="audioFile"></param>
        /// <returns></returns>
        string GetFolderWithinLibrary<T>(IDownloadableAudioFile<T> audioFile) where T : IAudioSource;

        /// <summary>
        /// Return the relative path (filename & folder within the appropriate ARES library) where the given audio file will be placed.
        /// </summary>
        /// <param name="audioFile"></param>
        /// <returns></returns>
        string GetPathWithinLibrary<T>(IDownloadableAudioFile<T> audioFile) where T : IAudioSource;

        /// <summary>
        /// Return the absolute path (filename & full path on the filesystem) where the given audio file will be placed.
        /// </summary>
        /// <param name="audioFile"></param>
        /// <returns></returns>
        string GetFullPath<T>(IDownloadableAudioFile<T> audioFile) where T : IAudioSource;
    }

    /// <summary>
    /// This class encapsulates information on the outcome of downloading an audio file.
    /// </summary>
    public class AudioDownloadResult
    {
        private ResultState m_State;
        private string m_Message;
        private Exception m_Cause;

        enum ResultState
        {
            SUCCESS,
            ERROR
        }

        public static AudioDownloadResult SUCCESS = new AudioDownloadResult(ResultState.SUCCESS, null);
        public static AudioDownloadResult ERROR(string message)
        {
            return new AudioDownloadResult(ResultState.ERROR, message);
        }
        public static AudioDownloadResult ERROR(string message, Exception cause)
        {
            return new AudioDownloadResult(ResultState.ERROR, message, cause);
        }

        private AudioDownloadResult(ResultState state, string message)
        {
            this.m_State = state;
            this.m_Message = message;
            this.m_Cause = null;
        }

        private AudioDownloadResult(ResultState state, string message, Exception cause)
        {
            this.m_State = state;
            this.m_Message = message;
            this.m_Cause = cause;
        }

    }
}