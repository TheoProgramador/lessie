"""Search for posts on LinkedIn."""

import os
from typing import Any
from urllib.parse import quote_plus

from linkedin_mcp_server.domain.models.responses import ScrapeResponse
from linkedin_mcp_server.domain.models.search import PostSearchResults
from linkedin_mcp_server.domain.parsers import parse_section
from linkedin_mcp_server.ports.auth import AuthPort
from linkedin_mcp_server.ports.browser import BrowserPort


class SearchPostsUseCase:
    """Search for posts/content on LinkedIn."""

    def __init__(self, browser: BrowserPort, auth: AuthPort, *, debug: bool = False):
        self._browser = browser
        self._auth = auth
        self._debug = debug

    async def execute(
        self,
        keywords: str,
        location: str | None = None,
        page_count: int | None = None,
        max_results: int | None = None,
        lightweight: bool = True,
    ) -> ScrapeResponse:
        await self._auth.ensure_authenticated()

        query = keywords if not location else f"{keywords} {location}"
        params = f"keywords={quote_plus(query)}"
        page_count = _resolve_int(page_count, "LINKEDIN_POST_SEARCH_PAGES", 1, 1, 5)
        max_results = _resolve_int(max_results, "LINKEDIN_POST_SEARCH_MAX_RESULTS", 12, 1, 50)
        last_url = ""
        all_posts = []
        seen_posts: set[str] = set()
        raw_pages: list[str] = []

        for page in range(1, page_count + 1):
            page_params = params if page == 1 else f"{params}&page={page}"
            url = f"https://www.linkedin.com/search/results/content/?{page_params}"
            if lightweight:
                content = await self._browser.extract_initial_page_html(url)
            else:
                content = await self._browser.extract_page_html(url)
            last_url = content.url

            if not content.html:
                continue

            parsed = parse_section(
                "search_results",
                content.html,
                entity_type="search_posts",
                include_raw=self._debug,
            )
            if self._debug:
                raw_pages.append(content.html)

            for post in getattr(parsed, "posts", []):
                post_key = post.post_url or f"{post.author_name}:{post.post_text}"
                if not post_key or post_key in seen_posts:
                    continue
                seen_posts.add(post_key)
                all_posts.append(post)
                if len(all_posts) >= max_results:
                    break

            if len(all_posts) >= max_results:
                break

        sections: dict[str, Any] = {
            "search_results": PostSearchResults(
                posts=all_posts,
                raw="\n<!-- linkedIn-mcp-page-break -->\n".join(raw_pages) if self._debug else None,
            )
        }

        return ScrapeResponse(url=last_url, sections=sections)


def _resolve_int(
    explicit_value: int | None,
    environment_name: str,
    default: int,
    minimum: int,
    maximum: int,
) -> int:
    if explicit_value is not None:
        return min(max(explicit_value, minimum), maximum)

    value = os.environ.get(environment_name, str(default))
    try:
        return min(max(int(value), minimum), maximum)
    except ValueError:
        return default
