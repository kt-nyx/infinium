import type { ApplicationClient } from "./client.js";
import type { ResultListRequest, ResultListResponse, ResultSummary } from "./generated/renderer-contract.generated.js";

export interface LogicalResultSummary {
  readonly item: ResultSummary;
  readonly logicalIndex: number;
}

export function virtualRowWindow<T>(items: readonly T[], firstVisibleRow: number, visibleRowCount: number): readonly { readonly item: T; readonly index: number }[] {
  const first = Math.max(0, Math.floor(firstVisibleRow));
  const count = Math.max(1, Math.min(30, Math.floor(visibleRowCount)));
  return items.slice(first, first + count).map((item, offset) => ({ item, index: first + offset }));
}

interface CachedPage {
  readonly startLogicalIndex: number;
  readonly response: Extract<ResultListResponse, { readonly outcome: "accepted" }>;
}

/** A forward cursor pager that retains at most five 100-item pages and never requests a full population. */
export class BoundedResultPager {
  private static readonly maximumCachedPages = 5;
  private readonly pages = new Map<number, CachedPage>();
  private request: Omit<ResultListRequest, "after_cursor"> | null = null;
  private pageIndex = -1;
  private acceptedCount = 0;
  private more = false;
  private outcome: ResultListResponse["outcome"] | null = null;

  public constructor(private readonly client: ApplicationClient) {}

  public get current(): readonly LogicalResultSummary[] {
    const page = this.pages.get(this.pageIndex);
    if (page === undefined) return [];
    return page.response.page.items.map((item, offset) => ({ item, logicalIndex: page.startLogicalIndex + offset }));
  }

  public get hasNext(): boolean { return this.pages.has(this.pageIndex + 1) || this.more; }
  public get hasPrevious(): boolean { return this.pages.has(this.pageIndex - 1); }
  public get cachedSummaryCount(): number { return [...this.pages.values()].reduce((count, page) => count + page.response.page.items.length, 0); }
  public get observedLogicalCount(): number { return this.acceptedCount; }
  public get accessibilitySetSize(): number { return this.more ? -1 : this.acceptedCount; }
  public get lastOutcome(): ResultListResponse["outcome"] | null { return this.outcome; }

  public async reset(request: Omit<ResultListRequest, "after_cursor">): Promise<ResultListResponse> {
    this.pages.clear();
    this.request = request;
    this.pageIndex = -1;
    this.acceptedCount = 0;
    this.more = false;
    this.outcome = null;
    return await this.load(undefined, 0, 0);
  }

  public async loadNext(): Promise<ResultListResponse | null> {
    const cached = this.pages.get(this.pageIndex + 1);
    if (cached !== undefined) {
      this.pageIndex++;
      this.updateActiveState(cached.response);
      return cached.response;
    }
    const current = this.pages.get(this.pageIndex);
    if (current === undefined || !current.response.page.has_more || current.response.page.next_cursor === undefined) return null;
    return await this.load(
      current.response.page.next_cursor,
      this.pageIndex + 1,
      current.startLogicalIndex + current.response.page.items.length);
  }

  public movePrevious(): boolean {
    const previous = this.pages.get(this.pageIndex - 1);
    if (previous === undefined) return false;
    this.pageIndex--;
    this.updateActiveState(previous.response);
    return true;
  }

  private async load(afterCursor: string | undefined, pageIndex: number, startLogicalIndex: number): Promise<ResultListResponse> {
    if (this.request === null) throw new Error("The result pager must be initialized before loading another page.");
    const response = await this.client.listResultItems({ ...this.request, ...(afterCursor === undefined ? {} : { after_cursor: afterCursor }) });
    this.outcome = response.outcome;
    if (response.outcome !== "accepted") return response;
    this.pageIndex = pageIndex;
    this.pages.set(pageIndex, { startLogicalIndex, response });
    this.acceptedCount = Math.max(this.acceptedCount, startLogicalIndex + response.page.items.length);
    this.updateActiveState(response);
    while (this.pages.size > BoundedResultPager.maximumCachedPages) {
      const oldest = Math.min(...this.pages.keys());
      this.pages.delete(oldest);
    }
    if (this.cachedSummaryCount > 500) throw new Error("The result pager exceeded its bounded summary cache.");
    return response;
  }

  private updateActiveState(response: Extract<ResultListResponse, { readonly outcome: "accepted" }>): void {
    this.outcome = response.outcome;
    this.more = response.page.has_more;
  }
}
