import React from 'react-native';
import { StyleSheet,View } from 'react-native';

const tintColor = '#2f95dc';

export const siteColors = {
  tintColor,
  tabIconDefault: '#ccc',
  tabIconSelected: tintColor,
  tabBar: '#fefefe',
  errorBackground: 'red',
  errorText: '#fff',
  warningBackground: '#EAEB5E',
  warningText: '#666804',
  noticeBackground: tintColor,
  noticeText: '#fff',

  borderColor:'#737373'
};



export const siteStyles =StyleSheet.create({
  warning:{
    //color:siteColors.warningText,
    backgroundColor:siteColors.warningBackground
  },
  Card:{
    margin:5,
    borderWidth: 1, 
    borderRadius: 10,
    borderColor: siteColors.tabIconDefault,
    padding:5,
    maxWidth:600
  },
  centered:{
    justifyContent: "center"
  },
  danger:{
    //color:siteColors.errorText,
    backgroundColor:siteColors.errorBackground,
  },
  separator: {
    marginVertical: 8,
    borderBottomColor: siteColors.borderColor,
    borderBottomWidth: 1,
  }
});

export function fromUTCDate(utcString:Date|string){
    const realDate = new Date(utcString);
    
    //means it's a min date value
    if(realDate.getFullYear()<=100)
      return '';
    return realDate.toLocaleString();
}


