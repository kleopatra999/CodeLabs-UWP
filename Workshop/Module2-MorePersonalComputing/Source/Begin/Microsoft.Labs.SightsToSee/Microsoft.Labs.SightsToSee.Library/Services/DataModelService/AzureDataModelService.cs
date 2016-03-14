﻿// offline sync

using Microsoft.Labs.SightsToSee.Library.Models;
using Microsoft.Labs.SightsToSee.Library.Services.AzureService;
using Microsoft.WindowsAzure.MobileServices;
using Microsoft.WindowsAzure.MobileServices.Files;
using Microsoft.WindowsAzure.MobileServices.SQLiteStore;
using Microsoft.WindowsAzure.MobileServices.Sync;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml.Controls;

namespace Microsoft.Labs.SightsToSee.Library.Services.DataModelService
{
#if !SQLITE
    public class AzureDataModelService : IDataModelService
    {
        MobileServiceClient client;

        MobileServiceCollection<Trip, Trip> trips;
        IMobileServiceSyncTable<Trip> tripTable;
        MobileServiceCollection<Sight, Sight> sights;
        IMobileServiceSyncTable<Sight> sightTable;
        MobileServiceCollection<SightFile, SightFile> sightFiles;
        IMobileServiceSyncTable<SightFile> sightFileTable;


        private async Task InitializeAsync()
        {
            if (client == null)
            {
                this.client = new MobileServiceClient("https://sights2see.azurewebsites.net");

                var store = new MobileServiceSQLiteStore("localstore.db");
                store.DefineTable<Trip>();
                store.DefineTable<Sight>();
                store.DefineTable<SightFile>();

                // Initialize file sync
                client.InitializeFileSyncContext(new AzureFileSyncHandler(this), store);

                //Initializes the SyncContext using the default IMobileServiceSyncHandler. 
                await client.SyncContext.InitializeAsync(store, StoreTrackingOptions.NotifyLocalAndServerOperations);

                tripTable = client.GetSyncTable<Trip>();
                sightTable = client.GetSyncTable<Sight>();
                sightFileTable = client.GetSyncTable<SightFile>();
            }
        }

        public async Task<Tuple<bool, string>> AuthenticateAsync()
        {
            await InitializeAsync();

            // Authentication required for the cloud storage
            bool success = false;
            string message = string.Empty;
            try
            {
                // Change 'MobileService' to the name of your MobileServiceClient instance.
                // Sign-in using Microsoft Account authentication.
                MobileServiceUser user = await this.client
                    .LoginAsync(MobileServiceAuthenticationProvider.MicrosoftAccount);
                Debug.WriteLine(message = string.Format("You are now signed in - {0}", user.UserId));

                success = true;
            }
            catch (InvalidOperationException)
            {
                message = "You must log in. Login Required";
            }

            return Tuple.Create(success, message);
        }

        public async Task<List<Trip>> LoadTripsAsync()
        {
            try
            {
                await InitializeAsync();
                await SyncAsync();
                return await tripTable.ToListAsync();
            }
            catch (Exception ex)
            {
                var errorString = "Load failed: " + ex.Message +
                                  "\n\nIf you are still in an offline scenario, " +
                                  "you can try your Pull again when connected with your Mobile Service.";
                await ShowError(errorString);
                throw;
            }
        }

        public async Task<List<Trip>> LoadTripsWithAttractionsAsync()
        {
            try
            {
                await InitializeAsync();
                var trips = await tripTable.ToListAsync();
                var sights = await sightTable.ToListAsync();

                foreach (var trip in trips)
                {
                    trip.Sights = new List<Sight>(sights.Where(a => a.TripId == trip.Id));
                }

                return trips;
            }
            catch (Exception ex)
            {
                var errorString = "Load failed: " + ex.Message +
                                  "\n\nIf you are still in an offline scenario, " +
                                  "you can try your Pull again when connected with your Mobile Service.";
                await ShowError(errorString);
                throw;
            }
        }

        public async Task InsertTripAsync(Trip trip)
        {
            try
            {
                await InitializeAsync();
                await tripTable.InsertAsync(trip);

                if (trip.Sights != null)
                {
                    foreach (var sight in trip.Sights)
                    {
                        await sightTable.InsertAsync(sight);

                        foreach (var sightFile in sight.SightFiles)
                        {
                            await sightFileTable.InsertAsync(sightFile);
                        }
                    }
                }
                await SyncAsync(); // offline sync     
            }
            catch (Exception ex)
            {
                var errorString = "Insert failed: " + ex.Message +
                                  "\n\nIf you are still in an offline scenario, " +
                                  "you can try your Pull again when connected with your Mobile Service.";
                await ShowError(errorString);
                throw;
            }
        }


        public async Task<Trip> LoadTripAsync(Guid tripId)
        {
            var trip = (await tripTable.Where(t => t.Id == tripId).ToListAsync()).Single();
            trip.Sights = await sightTable.Where(s => s.TripId == trip.Id).ToListAsync();
            foreach (var sight in trip.Sights)
            {
                sight.SightFiles = await sightFileTable.Where(s => s.SightId == sight.Id).ToListAsync();
                sight.Trip = trip;

                // Download files from cloud
                foreach (var sightFile in sight.SightFiles)
                {
                    await sightFileTable.GetFilesAsync(sightFile);
                }
            }
            return trip;
        }


        public async Task DeleteTripAsync(Trip trip)
        {
            try
            {
                await InitializeAsync();
                await tripTable.DeleteAsync(trip);

                await SyncAsync(); // offline sync     
            }
            catch (Exception ex)
            {
                var errorString = "Delete failed: " + ex.Message +
                                  "\n\nIf you are still in an offline scenario, " +
                                  "you can try your Pull again when connected with your Mobile Service.";
                await ShowError(errorString);
                throw;
            }
        }

        public async Task DeleteSightFileAsync(SightFile sightFile)
        {
            try
            {
                await InitializeAsync();
                await sightFileTable.DeleteAsync(sightFile);

                await SyncAsync(); // offline sync     
            }
            catch (Exception ex)
            {
                var errorString = "Delete failed: " + ex.Message +
                                  "\n\nIf you are still in an offline scenario, " +
                                  "you can try your Pull again when connected with your Mobile Service.";
                await ShowError(errorString);
                throw;
            }
        }

        public async Task DeleteSightAsync(Sight sight)
        {
            try
            {
                await InitializeAsync();
                await sightTable.DeleteAsync(sight);

                await SyncAsync(); // offline sync     
            }
            catch (Exception ex)
            {
                var errorString = "Delete failed: " + ex.Message +
                                  "\n\nIf you are still in an offline scenario, " +
                                  "you can try your Pull again when connected with your Mobile Service.";
                await ShowError(errorString);
                throw;
            }
        }

        public async Task SaveSightAsync(Sight sight)
        {
            try
            {
                await InitializeAsync();
                await sightTable.UpdateAsync(sight);

                await SyncAsync(); // offline sync     
            }
            catch (Exception ex)
            {
                var errorString = "Update failed: " + ex.Message +
                                  "\n\nIf you are still in an offline scenario, " +
                                  "you can try your Pull again when connected with your Mobile Service.";
                await ShowError(errorString);
                throw;
            }
        }

        public async Task SaveSightFileAsync(SightFile sightFile)
        {
            try
            {
                await InitializeAsync();
                await sightFileTable.InsertAsync(sightFile);

                // A 'SightFile' is actually the SightFile object that saves details about an attached file in a database table
                // and then there is the physical file itself
                if (sightFile.Uri?.StartsWith("ms-appdata") ?? false)
                {
                    // Optimisation for this lab - many images are shipped as content in the app package
                    // and therefore have ms-appx:/// Uris - don't sync these to the cloud, as it will put a big network surge
                    // when everyone starts the app for the first time
                    // So we only care about files with ms-appdata uri which have been created by the user - these we sync to the cloud
                    sightFile.File = await sightFileTable.AddFileAsync(sightFile, sightFile.FileName);

                }

                await SyncAsync(); // offline sync     
            }
            catch (Exception ex)
            {
                var errorString = "Insert SightFile failed: " + ex.Message +
                                  "\n\nIf you are still in an offline scenario, " +
                                  "you can try your Pull again when connected with your Mobile Service.";
                await ShowError(errorString);
                throw;
            }
        }

        public async Task UpdateSightFileAsync(SightFile sightFile)
        {
            try
            {
                await InitializeAsync();
                await sightFileTable.UpdateAsync(sightFile);

                await SyncAsync(); // offline sync     
            }
            catch (Exception ex)
            {
                var errorString = "Update SightFile failed: " + ex.Message +
                                  "\n\nIf you are still in an offline scenario, " +
                                  "you can try your Pull again when connected with your Mobile Service.";
                await ShowError(errorString);
                throw;
            }
        }

        public async Task<Sight> LoadSightAsync(Guid sightId)
        {
            var sight = (await sightTable.Where(s => s.Id == sightId).ToListAsync()).Single();
            var trip = (await tripTable.Where(t => t.Id == sight.TripId).ToListAsync()).Single();
            sight.Trip = trip;
            sight.SightFiles = await sightFileTable.Where(s => s.SightId == sight.Id).ToListAsync();
            return sight;
        }

        internal async Task DownloadFileAsync(MobileServiceFile file)
        {
            //// ****Workaround for CreateFile bug in library*****
            //string relfilePath = await FileHelper.GetLocalRelativeFilePathAsync(file.ParentId, file.Name);
            //await Windows.Storage.ApplicationData.Current.LocalFolder.CreateFileAsync(relfilePath, Windows.Storage.CreationCollisionOption.ReplaceExisting);

            var path = await FileHelper.GetLocalFilePathAsync(file.ParentId, file.Name);
            await sightFileTable.DownloadFileAsync(file, path);
        }

        private async Task SyncAsync()
        {
            string errorString = null;

            try
            {
                await client.SyncContext.PushAsync();
                await sightTable.PushFileChangesAsync();
                await tripTable.PullAsync("tripItems", tripTable.CreateQuery());
                await sightTable.PullAsync("attractionItems", sightTable.CreateQuery());
                await sightFileTable.PullAsync("attractionFileItems", sightFileTable.CreateQuery());
                //await sightTable.GetFilesAsync();
            }

            catch (MobileServicePushFailedException ex)
            {
                errorString = "Push failed because of sync errors. You may be offline.\nMessage: " +
                              ex.Message + "\nPushResult.Status: " + ex.PushResult.Status;
            }
            catch (Exception ex)
            {
                errorString = "Pull failed: " + ex.Message +
                              "\n\nIf you are still in an offline scenario, " +
                              "you can try your Pull again when connected with your Mobile Service.";
            }

            if (errorString != null)
            {
                await ShowError(errorString);
            }
        }

        private static async Task ShowError(string errorString)
        {
            var d = new ContentDialog
            {
                Content = errorString,
                Title = "Sync Error",
                PrimaryButtonText = "Ok",
                IsPrimaryButtonEnabled = true
            };
            await d.ShowAsync();
        }
    }
#endif
}