# Google Ecommerce Analytics module
Google Ecommerce Analytics module allows you to use the newly launched feature of Google Analytics – Enhanced Ecommerce. You can track the user behavior across your e-commerce store starting from product views to thank you page.

# Version History
## 1.3.0 
* Send ecommerce transaction in GA when order is created
* Reverse ecommerce transaction from GA when order is canceled
Note: Server-side transaction doesn't tracking user acquisition source.

## 1.1.0 
* Send E-Commerce transaction when order is created #3 and GoogleAnalytics TrackingId is set for store
* Added store settings for GoogleAnalytics TrackingId 
* Updated GoogleAnalyticsTracker to 4.4.0

## 1.0.0 
* Quick & Easy installation
* Supports Enhanced Ecommerce tracking with Google Tag Manager
* Measuring Product Impressions
* Measuring Views of Product Details
* Measuring Purchases

# Roadmap
* Captures Add to Cart & Product Clicks events
* Product Performance Report
* Product List Performance Report
* Captures Promo Views and Promo Clicks events
* Internal Promotion Report

![ecommerce-google-analytics-module-img1](https://cloud.githubusercontent.com/assets/7644848/17057938/bfd5cd42-501d-11e6-9a8a-9b50051d9178.PNG)

# Setup and installation

### 1. Download Google Tag Manager Container File
Download the <a href="https://github.com/VirtoCommerce/vc-module-google-ecommerce-analytics/raw/master/VirtoCommerce.GoogleEcommerceAnalyticsModule.Web/Content/gtm-virtocommerce_v1.json">container JSON file</a>.

### 2. Import JSON File into GTM
Log into your own <a href="http://www.google.com/tagmanager/">Google Tag Manager</a> container and head to the Admin section of the site. Under Container options, select Import Container.

### 3. Update With Your Own Tracking ID
Update the Variable named {{GA Property ID}} with your Google Analytics Tracking ID.

### 4. Preview & Publish
Use the Preview options to test this container on your own site. Try testing each of the events to make sure they’re working properly. If everything looks good, go ahead and publish!

### 5. Installing the module
* Automatically: in VC Manager go to Configuration -> Modules -> Store module -> Install
* Manually: download module zip package from https://github.com/VirtoCommerce/vc-module-google-ecommerce-analytics/releases. In VC Manager go to Configuration -> Modules -> Advanced -> upload module package -> Install.

### 6. Download Liquid snippet file
Download the snippet file <a href="https://raw.githubusercontent.com/VirtoCommerce/vc-module-google-ecommerce-analytics/master/VirtoCommerce.Storefront/App_Data/Themes/default/snippets/vc-google-ecommerce-analytics.liquid">vc-google-ecommerce-analytics.liquid</a> and copy it to snippet folder \VirtoCommerce.Storefront\App_Data\Themes\\{YourTheme}\snippets.

### 7. Include snippet into your themes
Paste this code {% include 'vc-google-ecommerce-analytics' %} into your themes so that it appears immediately after the opening \<body\> tag.

![image](https://cloud.githubusercontent.com/assets/7644848/17433536/92749548-5b05-11e6-8762-2a2e194e8e4e.png)

### 8. Settings
In VC Manager go to Browse -> STORES -> {YourStore} -> Ecommerce Google Analytics. Enable the plugin and insert your Google Tag Manager Id.

![image](https://cloud.githubusercontent.com/assets/7644848/17435851/bcf1fa28-5b13-11e6-8d24-8fd3b757051c.png)

### 9. Finish
To verify the correct integration you can use the Chrome extension <a href="https://chrome.google.com/webstore/detail/tag-assistant-by-google/kejbdjndbnbjgmefkgdddjlbokphdefk">Tag Assistant</a> from Google. 

# License
Copyright (c) Virto Solutions LTD.  All rights reserved.

Licensed under the Virto Commerce Open Software License (the "License"); you
may not use this file except in compliance with the License. You may
obtain a copy of the License at

http://virtocommerce.com/opensourcelicense

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or
implied.
