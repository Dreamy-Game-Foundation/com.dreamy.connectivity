#import <UIKit/UIKit.h>
typedef void (*DreamySettingsCallback)(int, int);
extern "C" void DreamyConnectivity_OpenSettings(int requestId, DreamySettingsCallback completion)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        NSURL *url = [NSURL URLWithString:UIApplicationOpenSettingsURLString];
        [[UIApplication sharedApplication] openURL:url options:@{} completionHandler:^(BOOL success) {
            completion(requestId, success ? 1 : 0);
        }];
    });
}
