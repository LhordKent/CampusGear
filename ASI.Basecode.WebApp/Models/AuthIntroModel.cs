using System;
using System.Collections.Generic;

namespace ASI.Basecode.WebApp.Models
{
    /// <summary>
    /// Left-hand intro beside the card on account pages (rendered by Views/Shared/_AuthIntro.cshtml).
    /// Its Title is the page's only h1; the card points at it with aria-labelledby="auth-title".
    /// </summary>
    public class AuthIntroModel
    {
        /// <summary>Small uppercase line above the headline, e.g. "Account recovery / Step 1 of 3".</summary>
        public string? Eyebrow { get; set; }

        /// <summary>First part of the headline (light).</summary>
        public string? Title { get; set; }

        /// <summary>Second part of the headline (amber), on its own line on wide screens.</summary>
        public string? TitleAccent { get; set; }

        public string? Lead { get; set; }

        /// <summary>Label above the steps or scope line, e.g. "Recovery steps".</summary>
        public string? StepsLabel { get; set; }

        /// <summary>Static one-line scope text shown instead of a step list (sign-in page).</summary>
        public string? Scope { get; set; }

        /// <summary>Ordered flow steps. Steps before <see cref="CurrentStep"/> show as done.</summary>
        public IReadOnlyList<string> Steps { get; set; } = Array.Empty<string>();

        /// <summary>1-based current step; 0 for none, past the end when the whole flow is complete.</summary>
        public int CurrentStep { get; set; }
    }
}
