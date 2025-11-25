/// <summary>
/// Contains attribution information for parts and models.
/// </summary>
/// <details>
/// This class stores attribution information such as author name.
/// </details>

using System;
using System.Collections.Generic;
using System.Linq;

namespace LDraw
{
    public class Attribution
    {
        /// <summary>
        /// The name of the author or creator.
        /// </summary>
        public List<string> AuthorNames { get; private set; }

        /// <summary>
        /// Initializes an empty instance of the <see cref="Attribution"/> class.
        /// </summary>
        public Attribution()
        {
            AuthorNames = new List<string>();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Attribution"/> class.
        /// </summary>
        /// <param name="authorName">The name of the author or creator.</param>
        public Attribution(string authorName)
        {
            AuthorNames = new List<string> { authorName };
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Attribution"/> class.
        /// </summary>
        /// <param name="authorNames">The list of author or creator names.</param>
        /// <remarks>
        /// This constructor allows for multiple authors to be specified.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="authorNames"/> is null.</exception>
        public Attribution(List<string> authorNames)
        {
            if (authorNames == null)
            {
                throw new ArgumentNullException(nameof(authorNames), "Author names list cannot be null.");
            }
            AuthorNames = new List<string>(authorNames);
        }

        /// <summary>
        /// Returns a string representation of the <see cref="Attribution"/> instance.
        /// </summary>
        /// <returns>A string listing the authors.</returns>
        public override string ToString()
        {
            return $"Attribution(Authors: {string.Join(", ", AuthorNames)})";
        }

        /// <summary>
        /// Adds an author name to the attribution.
        /// </summary>
        /// <param name="authorName">The name of the author to add.</param>
        public void AddAuthor(string authorName)
        {
            if (!string.IsNullOrEmpty(authorName))
            {
                AuthorNames.Add(authorName);
            }
        }

        /// <summary>
        /// Clears all author names from the attribution.
        /// </summary>
        public void Clear()
        {
            AuthorNames.Clear();
        }

        /// <summary>
        /// Checks if the attribution has any authors.
        /// </summary>
        /// <returns>True if the attribution is empty; otherwise, false.</returns>
        public bool IsEmpty()
        {
            return AuthorNames.Count == 0;
        }

        public List<string> UniqueAuthors()
        {
            return AuthorNames.Distinct().ToList();
        }
    }
}
