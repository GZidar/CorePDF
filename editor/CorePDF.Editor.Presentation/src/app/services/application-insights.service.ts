import { Injectable } from "@angular/core";

@Injectable()
export class ApplicationInsightsService {
    trackPageView(pageName?: string, url?: string): void {
        (<any>window).appInsights.trackPageView(pageName, url);
    }

    trackEvent(eventName: string): void {
        (<any>window).appInsights.trackPageView(eventName);
    }

    trackMetric(metricName: string, metricValue: any): void {
        (<any>window).appInsights.trackMetric(metricName, metricValue);
    }

    trackException(errorMessage: string, url?: string): void {
        (<any>window).appInsights.trackError(errorMessage, url);
    }
}