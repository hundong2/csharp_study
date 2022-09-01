﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using MvvmSample.Core.Helpers;

namespace MvvmSample.Core.ViewModels
{
    /// <summary>
    /// A base class for viewmodels for sample pages in the app.
    /// </summary>
    public class SamplePageViewModel : ObservableObject
    {
        private IReadOnlyDictionary<string, string>? texts;

        public SamplePageViewModel()
        {
            LoadDocsCommand = new AsyncRelayCommand<string>(LoadDocsAsync);
        }

        /// <summary>
        /// Gets the <see cref="IAsyncRelayCommand{T}"/> responsible for loading the source markdown docs.
        /// </summary>
        public IAsyncRelayCommand<string> LoadDocsCommand { get; }

        /// <summary>
        /// Gets or sets the collection of loaded paragraphs.
        /// </summary>
        public IReadOnlyDictionary<string, string>? Texts 
        { 
            get => texts; 
            set => SetProperty(ref texts, value); 
        }

        /// <summary>
        /// Gets the markdown for a specified paragraph from the docs page.
        /// </summary>
        /// <param name="key">The key of the paragraph to retrieve.</param>
        /// <returns>The text of the specified paragraph, or <see langword="null"/>.</returns>
        public string GetParagraph(string key)
        {
            return Texts != null && Texts.TryGetValue(key, out var value) ? value : string.Empty;
        }

        /// <summary>
        /// Implements the logic for <see cref="LoadDocsCommand"/>.
        /// </summary>
        /// <param name="name">The name of the docs file to load.</param>
        private async Task LoadDocsAsync(string? name)
        {
            // Skip if the loading has already started
            if (!(LoadDocsCommand.ExecutionTask is null)) return;

            string
                path = Path.Combine("Docs", $"{name!}.md"),
                text = await EmbeddedResources.GetStringAsync(Assembly.GetExecutingAssembly(), path);

            Texts = MarkdownHelper.GetParagraphs(text);

            OnPropertyChanged(nameof(GetParagraph));
        }
    }
}

