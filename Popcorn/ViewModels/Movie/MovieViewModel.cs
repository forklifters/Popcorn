﻿using System;
using System.Net;
using System.Net.Sockets;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;
using GalaSoft.MvvmLight.Ioc;
using GalaSoft.MvvmLight.Messaging;
using System.Threading;
using System.Threading.Tasks;
using Popcorn.Messaging;
using Popcorn.Models.Movie;
using Popcorn.Services.Movie;
using Popcorn.ViewModels.Download;
using Popcorn.ViewModels.Trailer;

namespace Popcorn.ViewModels.Movie
{
    /// <summary>
    /// Manage the movie
    /// </summary>
    public sealed class MovieViewModel : ViewModelBase
    {
        #region Properties

        #region Property -> MovieService

        /// <summary>
        /// The service used to interact with movies
        /// </summary>
        private MovieService MovieService { get; }

        #endregion

        #region Property -> Movie

        private MovieFull _movie = new MovieFull();

        /// <summary>
        /// The selected movie to show into the interface
        /// </summary>
        public MovieFull Movie
        {
            get { return _movie; }
            set { Set(() => Movie, ref _movie, value); }
        }

        #endregion

        #region Property -> IsMovieLoading

        private bool _isMovieLoading;

        /// <summary>
        /// Indicates if a movie is loading
        /// </summary>
        public bool IsMovieLoading
        {
            get { return _isMovieLoading; }
            set { Set(() => IsMovieLoading, ref _isMovieLoading, value); }
        }

        #endregion

        #region Property -> DownloadMovie

        private DownloadMovieViewModel _downloadMovie;

        /// <summary>
        /// View model which takes care of downloading the movie
        /// </summary>
        public DownloadMovieViewModel DownloadMovie
        {
            get { return _downloadMovie; }
            set { Set(() => DownloadMovie, ref _downloadMovie, value); }
        }

        #endregion

        #region Property -> Trailer

        /// <summary>
        /// View model which takes care of the movie's trailer
        /// </summary>
        private TrailerViewModel _trailer;

        public TrailerViewModel Trailer
        {
            get { return _trailer; }
            set { Set(() => Trailer, ref _trailer, value); }
        }

        #endregion

        #region Property -> IsTrailerLoading

        private bool _isTrailerLoading;

        /// <summary>
        /// Specify if a trailer is loading
        /// </summary>
        public bool IsTrailerLoading
        {
            get { return _isTrailerLoading; }
            set { Set(() => IsTrailerLoading, ref _isTrailerLoading, value); }
        }

        #endregion

        #region Property -> IsPlayingTrailer

        private bool _isPlayingTrailer;

        /// <summary>
        /// Specify if a trailer is loading
        /// </summary>
        public bool IsPlayingTrailer
        {
            get { return _isPlayingTrailer; }
            set { Set(() => IsPlayingTrailer, ref _isPlayingTrailer, value); }
        }

        #endregion

        #region Property -> IsDownloadingMovie

        private bool _isDownloadingMovie;

        /// <summary>
        /// Specify if a movie is downloading
        /// </summary>
        public bool IsDownloadingMovie
        {
            get { return _isDownloadingMovie; }
            set { Set(() => IsDownloadingMovie, ref _isDownloadingMovie, value); }
        }

        #endregion

        #region Property -> CancellationLoadingToken

        /// <summary>
        /// Token to cancel movie loading
        /// </summary>
        private CancellationTokenSource CancellationLoadingToken { get; }

        #endregion

        #region Property -> CancellationLoadingTrailerToken

        /// <summary>
        /// Token to cancel trailer loading
        /// </summary>
        private CancellationTokenSource CancellationLoadingTrailerToken { get; set; }

        #endregion

        #endregion

        #region Commands

        #region Command -> LoadMovieCommand

        /// <summary>
        /// Command used to load the movie
        /// </summary>
        public RelayCommand<MovieShort> LoadMovieCommand { get; private set; }

        #endregion

        #region Commands

        #region Command -> StopLoadingTrailerCommand

        /// <summary>
        /// Stop loading the trailer
        /// </summary>
        public RelayCommand StopLoadingTrailerCommand { get; private set; }

        #endregion

        #endregion

        #region Command -> PlayMovieCommand

        /// <summary>
        /// Command used to play the movie
        /// </summary>
        public RelayCommand PlayMovieCommand { get; private set; }

        #endregion

        #region Command -> PlayTrailerCommand

        /// <summary>
        /// Command used to play the trailer
        /// </summary>
        public RelayCommand PlayTrailerCommand { get; private set; }

        #endregion

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the MovieViewModel class.
        /// </summary>
        public MovieViewModel()
        {
            RegisterMessages();
            RegisterCommands();
            CancellationLoadingToken = new CancellationTokenSource();
            CancellationLoadingTrailerToken = new CancellationTokenSource();
            MovieService = SimpleIoc.Default.GetInstance<MovieService>();
        }

        #endregion

        #region Methods

        #region Method -> RegisterMessages

        /// <summary>
        /// Register messages
        /// </summary>
        private void RegisterMessages()
        {
            Messenger.Default.Register<StopPlayingTrailerMessage>(
                this,
                message => { StopPlayingTrailer(); });

            Messenger.Default.Register<StopPlayingMovieMessage>(
                this,
                message => { StopPlayingMovie(); });

            Messenger.Default.Register<ChangeLanguageMessage>(
                this,
                async message =>
                {
                    if (!string.IsNullOrEmpty(Movie?.ImdbCode))
                    {
                        await MovieService.TranslateMovieFullAsync(Movie);
                    }
                });
        }

        #endregion

        #region Method -> RegisterCommands

        /// <summary>
        /// Register commands
        /// </summary>
        private void RegisterCommands()
        {
            LoadMovieCommand = new RelayCommand<MovieShort>(async movie => { await LoadMovieAsync(movie); });

            PlayMovieCommand = new RelayCommand(() =>
            {
                IsDownloadingMovie = true;
                DownloadMovie = new DownloadMovieViewModel(Movie);
            });

            PlayTrailerCommand = new RelayCommand(async () =>
            {
                IsPlayingTrailer = true;
                IsTrailerLoading = true;
                Trailer = await TrailerViewModel.CreateAsync(Movie, CancellationLoadingTrailerToken);
                IsTrailerLoading = false;
            });

            StopLoadingTrailerCommand = new RelayCommand(StopLoadingTrailer);
        }

        #endregion

        #region Method -> LoadMovieAsync

        /// <summary>
        /// Get the requested movie
        /// </summary>
        /// <param name="movie">The movie to load</param>
        private async Task LoadMovieAsync(MovieShort movie)
        {
            Messenger.Default.Send(new LoadMovieMessage(movie));
            IsMovieLoading = true;
            try
            {
                Movie = await MovieService.GetMovieFullDetailsAsync(movie);
                IsMovieLoading = false;
                await MovieService.DownloadPosterImageAsync(Movie);
                await MovieService.DownloadDirectorImageAsync(Movie);
                await MovieService.DownloadActorImageAsync(Movie);
                await MovieService.DownloadBackgroundImageAsync(Movie);
            }
            catch (Exception ex) when (ex is SocketException || ex is WebException)
            {
                Messenger.Default.Send(new ManageExceptionMessage(ex));
            }
        }

        #endregion

        #region Method -> StopLoadingMovie

        /// <summary>
        /// Stop loading the movie
        /// </summary>
        private void StopLoadingMovie()
        {
            IsMovieLoading = false;
            CancellationLoadingToken?.Cancel();
        }

        #endregion

        #region Method -> StopPlayingTrailer

        /// <summary>
        /// Stop playing a trailer
        /// </summary>
        private void StopLoadingTrailer()
        {
            IsTrailerLoading = false;
            CancellationLoadingTrailerToken?.Cancel();
            CancellationLoadingTrailerToken?.Dispose();
            CancellationLoadingTrailerToken = new CancellationTokenSource();
            StopPlayingTrailer();
        }

        #endregion

        #region Method -> StopPlayingTrailer

        /// <summary>
        /// Stop playing a trailer
        /// </summary>
        private void StopPlayingTrailer()
        {
            IsPlayingTrailer = false;
            Trailer?.Cleanup();
            Trailer = null;
        }

        #endregion

        #region Method -> StopPlayingMovie

        /// <summary>
        /// Stop playing a movie
        /// </summary>
        private void StopPlayingMovie()
        {
            IsDownloadingMovie = false;
            DownloadMovie?.Cleanup();
            DownloadMovie = null;
        }

        #endregion

        public override void Cleanup()
        {
            StopLoadingMovie();
            CancellationLoadingToken?.Dispose();
            StopPlayingMovie();
            StopLoadingTrailer();
            CancellationLoadingTrailerToken?.Dispose();
            StopPlayingTrailer();
            base.Cleanup();
        }

        #endregion
    }
}