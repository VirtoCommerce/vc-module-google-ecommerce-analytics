# Overview

Google Analytics 4 module allows you to assign Google Analytics Measurement Id for a Store and measure traffic, ecommerce events to collect information about the shopping behaviour of your users.

![Google Analytics 4](https://developers.google.com/static/analytics/images/md_collection.svg)

## Key Features
1. Store Configuration.
1. Measure ecommerce with Vue B2B Theme and Virto Storefront via Google tag.
1. Ready for integration with other sales channels.

## Setup
First, [Create and configure Google Analytics 4 Account](https://support.google.com/analytics/answer/9304153)

Save your Measurement Id.

![How to find Measurement Id](https://storage.googleapis.com/support-kms-prod/4vzOnPW93ZjrGTZKfeIJYHXXPmpfCmc0UMHy)

1. Open Virto Commerce Back Office.
1. Select Store and Open Store Settings.
1. Find Google Analytics 4 section.
1. Enable Google Analytics and enter your Measurement Id.

![ga4 store settings](media/screen-ga4-store-settings.png)

Once you click Save for Store, the Google Analytics tracking will be activated.

## Integration with Virto Storefront
Virto Storefront and Vue B2B Theme has native integration with Google Analytics 4 module. 

We measures the following actions:

* Select an item from a category
* View product details
* Add/remove a product from a shopping cart
* Initiate the checkout process
* Make purchases or refunds
* Apply promotions

## Integration with Custom Application
You can use either Store settings or Rest API to request Google Analytics configuration for store.

## Settings
Google Analytics 4 module defines two store settings:

1. GoogleAnalytics4.EnableTracking
1. GoogleAnalytics4.MeasurementId

## Rest API

### Get Google Analytics Settings 

Endpoint: `/api/googleanalytics/{storeId}`

Method: `GET`

Request parameter: Store Id.

Response:

```json
{
  "enableTracking": true,
  "measurementId": "G-1234567890"
}
```

### Update Google Analytics Settings
Use Store API to provide management above Google Analytics Settings. 

## Troubleshoting 
[Enable debug mode](https://support.google.com/analytics/answer/7201382) so you can see events in realtime and more easily troubleshoot issues.
