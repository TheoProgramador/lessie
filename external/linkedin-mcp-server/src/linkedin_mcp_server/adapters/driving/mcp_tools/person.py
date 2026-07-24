"""Person-related MCP tool registrations."""

import traceback
from typing import Any

from fastmcp import Context, FastMCP

from linkedin_mcp_server.adapters.driving.error_mapping import map_domain_error
from linkedin_mcp_server.adapters.driving.serialization import (
    serialize_scrape_response,
)
from linkedin_mcp_server.application.scrape_person import ScrapePersonUseCase
from linkedin_mcp_server.application.search_people import SearchPeopleUseCase
from linkedin_mcp_server.application.search_posts import SearchPostsUseCase


def register_person_tools(
    mcp: FastMCP,
    scrape_person_uc: ScrapePersonUseCase,
    search_people_uc: SearchPeopleUseCase,
    search_posts_uc: SearchPostsUseCase,
) -> None:
    """Register person-related MCP tools."""

    @mcp.tool(
        name="get_person_profile",
        description=(
            "Get a specific person's LinkedIn profile.\n\n"
            "Args:\n"
            "    linkedin_username: LinkedIn username (e.g., 'satyanadella', 'jeffweiner08')\n"
            "    sections: Comma-separated list of extra sections to scrape.\n"
            "        The main profile page is always included.\n"
            "        Available sections: experience, education, interests, honors, "
            "languages, contact_info, posts\n"
            "        Default (None) scrapes only the main profile page."
        ),
    )
    async def get_person_profile(
        linkedin_username: str,
        ctx: Context,
        sections: str | None = None,
    ) -> dict[str, Any]:
        try:
            result = await scrape_person_uc.execute(linkedin_username, sections)
            return serialize_scrape_response(result)
        except Exception as e:
            map_domain_error(e, "get_person_profile")

    @mcp.tool(
        name="search_people",
        description=(
            "Search for people on LinkedIn.\n\n"
            "Args:\n"
            "    keywords: Search keywords (e.g., 'product manager', 'ML engineer at Meta')\n"
            "    location: Optional location filter (e.g., 'London', 'Berlin')"
        ),
    )
    async def search_people(
        keywords: str,
        ctx: Context,
        location: str | None = None,
    ) -> dict[str, Any]:
        try:
            await ctx.info(
                "LinkedIn people search started.",
                logger_name="linkedin.search_people",
                extra={"keywords": keywords, "location": location},
            )
            await ctx.report_progress(5, 100, "Starting LinkedIn people search.")

            result = await search_people_uc.execute(keywords, location)
            people = getattr(result.sections.get("search_results"), "people", [])
            people_count = len(people)

            await ctx.info(
                f"LinkedIn people search parsed {people_count} people.",
                logger_name="linkedin.search_people",
                extra={"peopleCount": people_count},
            )
            await ctx.report_progress(
                100,
                100,
                f"LinkedIn people search parsed {people_count} people.",
            )

            return serialize_scrape_response(result)
        except Exception as e:
            error_message = f"{type(e).__name__}: {e}" if str(e) else type(e).__name__
            await ctx.error(
                f"LinkedIn people search failed: {error_message}",
                logger_name="linkedin.search_people",
                extra={"error": error_message, "traceback": traceback.format_exc()},
            )
            map_domain_error(e, "search_people")

    @mcp.tool(
        name="search_posts",
        description=(
            "Search for posts/content on LinkedIn.\n\n"
            "Args:\n"
            "    keywords: Search keywords (e.g., 'hiring .NET remote', 'recruiter angular')\n"
            "    location: Optional location text appended to the search keywords.\n"
            "    page_count: Optional number of search result pages to inspect.\n"
            "    max_results: Optional maximum posts to return.\n"
            "    lightweight: When true, avoids deep scrolling and extracts the first "
            "visible batch."
        ),
    )
    async def search_posts(
        keywords: str,
        ctx: Context,
        location: str | None = None,
        page_count: int | None = None,
        max_results: int | None = None,
        lightweight: bool = True,
    ) -> dict[str, Any]:
        try:
            await ctx.info(
                "LinkedIn posts search started.",
                logger_name="linkedin.search_posts",
                extra={
                    "keywords": keywords,
                    "location": location,
                    "pageCount": page_count,
                    "maxResults": max_results,
                    "lightweight": lightweight,
                },
            )
            await ctx.report_progress(5, 100, "Starting LinkedIn posts search.")

            result = await search_posts_uc.execute(
                keywords,
                location,
                page_count=page_count,
                max_results=max_results,
                lightweight=lightweight,
            )
            posts = getattr(result.sections.get("search_results"), "posts", [])
            post_count = len(posts)

            await ctx.info(
                f"LinkedIn posts search parsed {post_count} posts.",
                logger_name="linkedin.search_posts",
                extra={"postCount": post_count},
            )
            await ctx.report_progress(100, 100, f"LinkedIn posts search parsed {post_count} posts.")

            return serialize_scrape_response(result)
        except Exception as e:
            error_message = f"{type(e).__name__}: {e}" if str(e) else type(e).__name__
            await ctx.error(
                f"LinkedIn posts search failed: {error_message}",
                logger_name="linkedin.search_posts",
                extra={"error": error_message, "traceback": traceback.format_exc()},
            )
            map_domain_error(e, "search_posts")
