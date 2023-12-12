import React from "react";
interface IProps {
  type: string;
  style?: any;
}
const IconFont: React.FC<IProps> = (props) => {
  return (
    <span
      className={`icon iconfont ${props.type}`}
      style={props.style}
    ></span>
  );
};

export default IconFont;
