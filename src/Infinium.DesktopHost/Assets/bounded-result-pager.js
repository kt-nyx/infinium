export function virtualRowWindow(items, firstVisibleRow, visibleRowCount) {
    const first = Math.max(0, Math.floor(firstVisibleRow));
    const count = Math.max(1, Math.min(30, Math.floor(visibleRowCount)));
    return items.slice(first, first + count).map((item, offset) => ({ item, index: first + offset }));
}
/** A forward cursor pager that retains at most five 100-item pages and never requests a full population. */
export class BoundedResultPager {
    client;
    static maximumCachedPages = 5;
    pages = new Map();
    request = null;
    pageIndex = -1;
    acceptedCount = 0;
    more = false;
    outcome = null;
    constructor(client) {
        this.client = client;
    }
    get current() {
        const page = this.pages.get(this.pageIndex);
        if (page === undefined)
            return [];
        return page.response.page.items.map((item, offset) => ({ item, logicalIndex: page.startLogicalIndex + offset }));
    }
    get hasNext() { return this.pages.has(this.pageIndex + 1) || this.more; }
    get hasPrevious() { return this.pages.has(this.pageIndex - 1); }
    get cachedSummaryCount() { return [...this.pages.values()].reduce((count, page) => count + page.response.page.items.length, 0); }
    get observedLogicalCount() { return this.acceptedCount; }
    get accessibilitySetSize() { return this.more ? -1 : this.acceptedCount; }
    get lastOutcome() { return this.outcome; }
    async reset(request) {
        this.pages.clear();
        this.request = request;
        this.pageIndex = -1;
        this.acceptedCount = 0;
        this.more = false;
        this.outcome = null;
        return await this.load(undefined, 0, 0);
    }
    async loadNext() {
        const cached = this.pages.get(this.pageIndex + 1);
        if (cached !== undefined) {
            this.pageIndex++;
            this.updateActiveState(cached.response);
            return cached.response;
        }
        const current = this.pages.get(this.pageIndex);
        if (current === undefined || !current.response.page.has_more || current.response.page.next_cursor === undefined)
            return null;
        return await this.load(current.response.page.next_cursor, this.pageIndex + 1, current.startLogicalIndex + current.response.page.items.length);
    }
    movePrevious() {
        const previous = this.pages.get(this.pageIndex - 1);
        if (previous === undefined)
            return false;
        this.pageIndex--;
        this.updateActiveState(previous.response);
        return true;
    }
    async load(afterCursor, pageIndex, startLogicalIndex) {
        if (this.request === null)
            throw new Error("The result pager must be initialized before loading another page.");
        const response = await this.client.listResultItems({ ...this.request, ...(afterCursor === undefined ? {} : { after_cursor: afterCursor }) });
        this.outcome = response.outcome;
        if (response.outcome !== "accepted")
            return response;
        this.pageIndex = pageIndex;
        this.pages.set(pageIndex, { startLogicalIndex, response });
        this.acceptedCount = Math.max(this.acceptedCount, startLogicalIndex + response.page.items.length);
        this.updateActiveState(response);
        while (this.pages.size > BoundedResultPager.maximumCachedPages) {
            const oldest = Math.min(...this.pages.keys());
            this.pages.delete(oldest);
        }
        if (this.cachedSummaryCount > 500)
            throw new Error("The result pager exceeded its bounded summary cache.");
        return response;
    }
    updateActiveState(response) {
        this.outcome = response.outcome;
        this.more = response.page.has_more;
    }
}
