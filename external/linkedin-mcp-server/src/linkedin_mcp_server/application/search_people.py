"""Search for people on LinkedIn."""

import os
from typing import Any
from urllib.parse import quote_plus

from linkedin_mcp_server.domain.models.search import PeopleSearchResults
from linkedin_mcp_server.domain.models.responses import ScrapeResponse
from linkedin_mcp_server.domain.parsers import parse_section
from linkedin_mcp_server.ports.auth import AuthPort
from linkedin_mcp_server.ports.browser import BrowserPort


class SearchPeopleUseCase:
    """Search for people on LinkedIn."""

    def __init__(self, browser: BrowserPort, auth: AuthPort, *, debug: bool = False):
        self._browser = browser
        self._auth = auth
        self._debug = debug

    async def execute(
        self,
        keywords: str,
        location: str | None = None,
    ) -> ScrapeResponse:
        await self._auth.ensure_authenticated()

        params = f"keywords={quote_plus(keywords)}"
        if location:
            params += f"&location={quote_plus(location)}"

        page_count = _get_people_search_page_count()
        last_url = ""
        all_people = []
        seen_profiles: set[str] = set()
        raw_pages: list[str] = []

        for page in range(1, page_count + 1):
            page_params = params if page == 1 else f"{params}&page={page}"
            url = f"https://www.linkedin.com/search/results/people/?{page_params}"
            content = await self._browser.extract_page_html(url)
            last_url = content.url

            if not content.html:
                continue

            parsed = parse_section(
                "search_results",
                content.html,
                entity_type="search_people",
                include_raw=self._debug,
            )
            if self._debug:
                raw_pages.append(content.html)

            for person in getattr(parsed, "people", []):
                profile_key = person.profile_url or person.linkedin_username or person.name
                if not profile_key or profile_key in seen_profiles:
                    continue
                seen_profiles.add(profile_key)
                all_people.append(person)

        sections: dict[str, Any] = {
            "search_results": PeopleSearchResults(
                people=all_people,
                raw="\n<!-- linkedIn-mcp-page-break -->\n".join(raw_pages) if self._debug else None,
            )
        }

        return ScrapeResponse(url=last_url, sections=sections)


def _get_people_search_page_count() -> int:
    value = os.environ.get("LINKEDIN_PEOPLE_SEARCH_PAGES", "3")
    try:
        return min(max(int(value), 1), 10)
    except ValueError:
        return 3
