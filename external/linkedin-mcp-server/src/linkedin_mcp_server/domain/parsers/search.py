"""Search results HTML parsers.

All functions receive HTML and return typed models.
Handles both LinkedIn's SDUI layout (people search) and classic Ember
layout (job search).
"""

import re
from urllib.parse import unquote

from linkedin_mcp_server.domain.models.search import (
    JobSearchResultEntry,
    JobSearchResults,
    PeopleSearchResults,
    PersonSearchResult,
    PostSearchResult,
    PostSearchResults,
)
from linkedin_mcp_server.domain.parsers.common import (
    JOB_VIEW_RE,
    soup,
    text,
)

_PROFILE_URL_RE = re.compile(r"(?:https?://(?:www\.)?linkedin\.com)?/in/([^/?#]+)", re.I)
_POST_URL_RE = re.compile(
    r"(?:https?://(?:www\.)?linkedin\.com)?/(?:feed/update/urn:li:activity:\d+|posts/[^?\"'#<\s]+)"
    r"|highlightedUpdateUrn=(?:urn%3Ali%3Aactivity%3A|urn:li:activity:)\d+",
    re.I,
)
_ACTIVITY_URN_RE = re.compile(r"urn:li:activity:(\d+)", re.I)
_CONNECTION_DEGREE_RE = re.compile(r"\b(\d+(?:st|nd|rd|th)|\d+º)\b", re.I)


# ── People Search parser ─────────────────────────────────────────────────────


def parse_search_results_people(html: str, *, include_raw: bool = False) -> PeopleSearchResults:
    """Parse people search results page HTML.

    Extracts list of PersonSearchResult from SDUI search result cards.
    Each card is identified by data-view-name="people-search-result".
    """
    s = soup(html, parser="html.parser")
    results: list[PersonSearchResult] = []

    cards = s.find_all(attrs={"data-view-name": "people-search-result"})

    for card in cards:
        # Profile URL and username from the main <a> link
        profile_link = card.find(
            "a",
            attrs={"data-view-name": "search-result-lockup-title"},
        )
        profile_url: str | None = None
        linkedin_username: str | None = None
        name: str = ""

        if profile_link:
            name = text(profile_link) or ""
            href = profile_link.get("href", "")
            if href:
                profile_url = href
                m = _PROFILE_URL_RE.search(href)
                if m:
                    linkedin_username = m.group(1)

        if not name:
            continue

        # Connection degree from <span class="_45102191">
        connection_degree = ""
        degree_container = card.find("span", class_=lambda c: c and "_45102191" in c)
        if degree_container:
            degree_text = text(degree_container)
            if degree_text:
                # Extract "1st", "2nd", "3rd" etc.
                m = re.search(r"(\d+(?:st|nd|rd|th))", degree_text)
                if m:
                    connection_degree = m.group(1)

        # Profile image from <figure> with aria-label
        profile_image_url: str | None = None
        figure = card.find("figure", attrs={"data-view-name": "image"})
        if figure:
            img = figure.find("img")
            if img:
                src = img.get("src", "")
                if src and "profile-displayphoto" in src:
                    profile_image_url = src

        # Headline — first <p> with _37677861 class in name's parent
        headline: str | None = None
        location: str | None = None

        # The listitem div contains the name + headline + location in order
        listitem = card.find("div", attrs={"role": "listitem"})
        if listitem:
            # Find all <p> with _37677861 class that are direct text content
            info_divs = listitem.find_all(
                "div",
                class_=lambda c: (
                    c
                    and "_04bda81b" in c
                    and "_9dfef8a0" in c
                    and "_837488b5" in c
                ),
            )
            for i, div in enumerate(info_divs):
                p = div.find("p", class_=lambda c: c and "_37677861" in c)
                if p:
                    txt = text(p)
                    if txt:
                        if i == 0:
                            headline = txt
                        elif i == 1:
                            location = txt

        # Mutual connections from social proof insight
        mutual_connections: str | None = None
        social_proof_links = card.find_all(
            "a",
            attrs={"data-view-name": "search-result-social-proof-insight"},
        )
        for sp_link in social_proof_links:
            sp_text = text(sp_link)
            if sp_text and "mutual connection" in sp_text.lower():
                mutual_connections = sp_text

        # Followers from social proof
        followers: str | None = None
        for sp_link in social_proof_links:
            sp_text = text(sp_link)
            if sp_text and "follower" in sp_text.lower():
                followers = sp_text

        results.append(
            PersonSearchResult(
                name=name,
                connection_degree=connection_degree,
                headline=headline,
                location=location,
                mutual_connections=mutual_connections,
                followers=followers,
                profile_url=profile_url,
                linkedin_username=linkedin_username,
                profile_image_url=profile_image_url,
            )
        )

    if not results:
        results = _parse_people_from_profile_links(s)

    return PeopleSearchResults(
        people=results,
        raw=html if include_raw else None,
    )


def _parse_people_from_profile_links(s) -> list[PersonSearchResult]:
    """Fallback parser for LinkedIn's current SDUI people-search layout."""
    results: list[PersonSearchResult] = []
    seen_usernames: set[str] = set()

    for profile_link in s.find_all("a", href=lambda href: href and _PROFILE_URL_RE.search(href)):
        href = profile_link.get("href", "")
        match = _PROFILE_URL_RE.search(href)
        if not match:
            continue

        linkedin_username = match.group(1)
        if linkedin_username in seen_usernames:
            continue

        container = _find_people_result_container(profile_link)
        name = _clean_profile_name(text(profile_link) or "")
        if not name:
            name = _clean_profile_name(_first_profile_name(container) or "")
        if not name:
            continue

        seen_usernames.add(linkedin_username)
        container_text = text(container) or ""
        headline, location = _extract_people_result_lines(container, name)

        results.append(
            PersonSearchResult(
                name=name,
                connection_degree=_extract_connection_degree(container_text),
                headline=headline,
                location=location,
                profile_url=_normalize_profile_url(href),
                linkedin_username=linkedin_username,
                profile_image_url=_extract_profile_image_url(container),
            )
        )

    return results


def _find_people_result_container(profile_link):
    current = profile_link
    for _ in range(8):
        if current is None:
            break
        if current.get("role") == "listitem":
            return current
        component_key = current.get("componentkey", "")
        if isinstance(component_key, str) and component_key.startswith("SearchResults_"):
            return current
        current = current.parent
    return profile_link.parent


def _first_profile_name(container) -> str | None:
    if container is None:
        return None
    link = container.find("a", href=lambda href: href and _PROFILE_URL_RE.search(href))
    return text(link)


def _clean_profile_name(value: str) -> str:
    cleaned = value.split("•", 1)[0]
    cleaned = re.sub(r"\b\d+(?:st|nd|rd|th)\b", "", cleaned, flags=re.I)
    cleaned = re.sub(r"\b\d+º\b", "", cleaned)
    cleaned = cleaned.replace("Usuário verificado", "").replace("User verified", "")
    cleaned = re.sub(r"\s+", " ", cleaned).strip()
    parts = cleaned.split()
    half = len(parts) // 2
    if half > 0 and len(parts) % 2 == 0 and parts[:half] == parts[half:]:
        cleaned = " ".join(parts[:half])
    if len(cleaned) > 100:
        cleaned = cleaned[:100].strip()
    return cleaned


def _extract_connection_degree(value: str) -> str:
    match = _CONNECTION_DEGREE_RE.search(value)
    return match.group(1) if match else ""


def _extract_people_result_lines(container, name: str) -> tuple[str | None, str | None]:
    if container is None:
        return None, None

    candidates = _candidate_result_texts(container, name)

    headline = None
    location = None
    for candidate in candidates:
        lower = candidate.lower()
        if not headline and any(token in lower for token in ("recruit", "developer", "engineer", "manager", "@", "|")):
            headline = candidate
            continue
        if not location and any(token in lower for token in ("brasil", "brazil", "remote", "são paulo", "rio de janeiro")):
            location = candidate
            continue

    if not headline and candidates:
        headline = candidates[0]
    if not location and len(candidates) > 1:
        location = candidates[1]

    return headline, location


def _candidate_result_texts(container, name: str) -> list[str]:
    candidates: list[str] = []
    for tag_names in (["p"], ["span"], ["div"]):
        for paragraph in container.find_all(tag_names):
            cleaned = _clean_result_text(text(paragraph) or "", name)
            if not cleaned or cleaned in candidates:
                continue
            if "convidar " in cleaned.lower() or "connect" in cleaned.lower():
                continue
            if _PROFILE_URL_RE.search(cleaned):
                continue
            candidates.append(cleaned)
        if len(candidates) >= 2:
            break
    return candidates


def _clean_result_text(value: str, name: str) -> str:
    cleaned = re.sub(r"\s+", " ", value).strip()
    cleaned = cleaned.replace("Usuário verificado", "").replace("User verified", "")
    cleaned = re.sub(rf"^{re.escape(name)}\s*[•·-]?\s*", "", cleaned).strip()
    cleaned = re.sub(r"^\d+(?:st|nd|rd|th|º)\s*", "", cleaned, flags=re.I).strip()
    cleaned = cleaned.strip("•·- ")
    return "" if cleaned == name else cleaned


def _extract_profile_image_url(container) -> str | None:
    if container is None:
        return None
    img = container.find("img", src=lambda src: src and "profile-displayphoto" in src)
    return img.get("src") if img else None


def _normalize_profile_url(href: str) -> str:
    match = _PROFILE_URL_RE.search(href)
    if not match:
        return href
    return f"https://www.linkedin.com/in/{match.group(1)}/"


# ── Job Search parser ────────────────────────────────────────────────────────


def parse_search_results_posts(html: str, *, include_raw: bool = False) -> PostSearchResults:
    """Parse LinkedIn content search results."""
    s = soup(html, parser="html.parser")
    results: list[PostSearchResult] = []
    seen_posts: set[str] = set()

    for post_link in s.find_all("a", href=lambda href: href and _POST_URL_RE.search(href)):
        container = _find_post_result_container(post_link)
        _append_post_result(results, seen_posts, container, post_link)

    for container in s.find_all(attrs={"role": "listitem"}):
        if not _looks_like_post_result(container):
            continue
        _append_post_result(results, seen_posts, container, None)

    return PostSearchResults(
        posts=results,
        raw=html if include_raw else None,
    )


def _append_post_result(
    results: list[PostSearchResult],
    seen_posts: set[str],
    container,
    post_link,
) -> None:
    container_text = _clean_post_text(text(container) or "")
    author_link = _find_post_author_link(container)
    author_name = _clean_profile_name(text(author_link) or "") if author_link else None
    author_profile_url = _normalize_profile_url(author_link.get("href", "")) if author_link else None
    post_url = _extract_post_url(container, post_link)
    post_text = _extract_post_body(container_text, author_name)

    if not post_text and post_link is not None and text(post_link):
        post_text = _clean_post_text(text(post_link) or "")

    if not author_name and not post_text:
        return

    post_key = post_url or f"{author_profile_url}:{author_name}:{post_text}"
    if not post_key or post_key in seen_posts:
        return

    seen_posts.add(post_key)
    results.append(
        PostSearchResult(
            author_name=author_name,
            author_headline=_extract_post_author_headline(container, author_name),
            author_profile_url=author_profile_url,
            post_text=post_text,
            post_url=post_url,
            posted_at=_extract_posted_at(container_text),
            social_text=_extract_social_text(container_text),
        )
    )


def _looks_like_post_result(container) -> bool:
    component_key = container.get("componentkey", "")
    if isinstance(component_key, str) and "FeedType_FLAGSHIP_SEARCH" in component_key:
        return True

    container_text = _clean_post_text(text(container) or "").lower()
    return "feed" in container_text and (
        "publica" in container_text
        or "post" in container_text
    )


def _extract_post_url(container, post_link) -> str | None:
    if post_link is not None:
        post_url = _normalize_linkedin_post_url(post_link.get("href", ""))
        if post_url:
            return post_url

    if container is None:
        return None

    for link in container.find_all("a", href=True):
        post_url = _normalize_linkedin_post_url(link.get("href", ""))
        if post_url:
            return post_url

    return None


def _find_post_result_container(post_link):
    current = post_link
    for _ in range(10):
        if current is None:
            break
        if current.get("role") == "listitem":
            return current
        component_key = current.get("componentkey", "")
        if isinstance(component_key, str) and component_key.startswith("SearchResults_"):
            return current
        classes = current.get("class", [])
        if any("search" in str(class_name).lower() and "result" in str(class_name).lower() for class_name in classes):
            return current
        current = current.parent
    return post_link.parent


def _find_post_author_link(container):
    if container is None:
        return None

    profile_links = container.find_all("a", href=lambda href: href and _PROFILE_URL_RE.search(href))
    for profile_link in profile_links:
        if _clean_profile_name(text(profile_link) or ""):
            return profile_link

    return profile_links[0] if profile_links else None


def _extract_post_author_headline(container, author_name: str | None) -> str | None:
    if container is None:
        return None

    candidates: list[str] = []
    for paragraph in container.find_all(["p", "span", "div"]):
        cleaned = _clean_post_text(text(paragraph) or "")
        if not cleaned or cleaned in candidates:
            continue
        if author_name and cleaned == author_name:
            continue
        lower = cleaned.lower()
        if any(token in lower for token in ("like", "comment", "repost", "reaction", "visualizar", "curtir")):
            continue
        candidates.append(cleaned)
        if len(candidates) >= 3:
            break

    return candidates[0] if candidates else None


def _extract_post_body(container_text: str, author_name: str | None) -> str | None:
    if not container_text:
        return None

    cleaned = container_text
    cleaned = re.sub(r"^Publica\S+\s+no\s+feed\b", "", cleaned, flags=re.I).strip()
    if author_name:
        cleaned = re.sub(rf"^{re.escape(author_name)}\b", "", cleaned).strip()

    cleaned = re.sub(
        r"\b(?:view|visualizar|curtir|like|comment|coment.rio|repost|share|compartilhar)\b.*$",
        "",
        cleaned,
        flags=re.I,
    ).strip()
    cleaned = cleaned.strip(" -.")

    if len(cleaned) > 700:
        cleaned = cleaned[:700].rsplit(" ", 1)[0].strip()

    return cleaned or None


def _extract_posted_at(value: str) -> str | None:
    match = re.search(
        r"\b(?:\d+\s*(?:s|min|m|h|d|w|sem|mo|meses?|dias?|horas?)|now|agora)\b",
        value,
        flags=re.I,
    )
    return match.group(0) if match else None


def _extract_social_text(value: str) -> str | None:
    match = re.search(
        r"(\d+\s+(?:comments?|coment.rios?|reposts?|shares?|reactions?))",
        value,
        flags=re.I,
    )
    return match.group(1) if match else None


def _clean_post_text(value: str) -> str:
    return re.sub(r"\s+", " ", value).strip()


def _normalize_linkedin_post_url(href: str) -> str | None:
    decoded = unquote(href)
    activity_match = _ACTIVITY_URN_RE.search(decoded)
    if activity_match:
        return f"https://www.linkedin.com/feed/update/urn:li:activity:{activity_match.group(1)}"

    match = _POST_URL_RE.search(href)
    if not match:
        return None

    url = match.group(0)
    if url.startswith("highlightedUpdateUrn="):
        activity_match = _ACTIVITY_URN_RE.search(unquote(url))
        return (
            f"https://www.linkedin.com/feed/update/urn:li:activity:{activity_match.group(1)}"
            if activity_match
            else None
        )

    if url.startswith("http"):
        return url

    return f"https://www.linkedin.com{url}"


def parse_search_results_jobs(html: str, *, include_raw: bool = False) -> JobSearchResults:
    """Parse job search results page HTML.

    Uses the classic Ember layout with job-card-container divs.
    Extracts job_id, title, company, location, insight, and metadata.
    """
    s = soup(html, parser="html.parser")
    results: list[JobSearchResultEntry] = []

    # Total results from the header subtitle
    total_results: str | None = None
    subtitle = s.find(
        "div",
        class_=lambda c: c and "jobs-search-results-list__subtitle" in c,
    )
    if subtitle:
        total_results = text(subtitle)

    # Each job card has a data-job-id attribute
    cards = s.find_all(
        "div",
        attrs={"data-job-id": True},
        class_=lambda c: c and "job-card-container" in c,
    )

    for card in cards:
        job_id = card.get("data-job-id")

        # Title from the link's aria-label
        title: str | None = None
        job_url: str | None = None
        title_link = card.find("a", class_=lambda c: c and "job-card-container__link" in c)
        if title_link:
            label = title_link.get("aria-label", "")
            if label:
                # Clean "with verification" suffix
                title = re.sub(r"\s+with verification$", "", label).strip()
            href = title_link.get("href", "")
            if href:
                m = JOB_VIEW_RE.search(href)
                job_url = f"https://www.linkedin.com/jobs/view/{m.group(1)}/" if m else None

        # Company from artdeco-entity-lockup__subtitle
        company: str | None = None
        company_el = card.find(
            "div",
            class_=lambda c: c and "artdeco-entity-lockup__subtitle" in c,
        )
        if company_el:
            company = text(company_el)

        # Location from metadata wrapper
        location: str | None = None
        location_li = card.find(
            "li",
            class_=lambda c: c and "pJCTyyZHJEwdnAZhBTBVMaBZjcFmTQ" in c,
        )
        if location_li:
            location = text(location_li)

        # Insight text (e.g., "Actively reviewing applicants")
        insight: str | None = None
        insight_el = card.find("div", class_="job-card-container__job-insight-text")
        if insight_el:
            insight = text(insight_el)

        # Footer metadata items (Viewed, Promoted, Be an early applicant)
        metadata_parts: list[str] = []
        footer_items = card.find_all(
            "li",
            class_=lambda c: c and "job-card-container__footer-item" in c,
        )
        for fi in footer_items:
            fi_text = text(fi)
            if fi_text:
                metadata_parts.append(fi_text)
        metadata = " · ".join(metadata_parts) if metadata_parts else None

        # Company logo URL from the card's logo image
        company_logo_url: str | None = None
        logo_div = card.find("div", class_=lambda c: c and "job-card-list__logo" in c)
        if logo_div:
            img = logo_div.find("img")
            if img:
                src = img.get("src", "")
                if src:
                    company_logo_url = src

        results.append(
            JobSearchResultEntry(
                title=title,
                company=company,
                location=location,
                job_id=job_id,
                job_url=job_url,
                company_logo_url=company_logo_url,
                insight=insight,
                metadata=metadata,
            )
        )

    return JobSearchResults(
        total_results=total_results,
        jobs=results,
        raw=html if include_raw else None,
    )
